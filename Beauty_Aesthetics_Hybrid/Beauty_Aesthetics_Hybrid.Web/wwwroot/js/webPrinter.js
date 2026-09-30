window.webPrinter = (function () {
    let activeBtDevice = null;
    let activeGattServer = null;
    let activeSerialPort = null;

    const optionalServices = [
        '00001101-0000-1000-8000-00805f9b34fb', // Standard SPP
        '000018f0-0000-1000-8000-00805f9b34fb', // Thermal Printer GATT Service
        'e7810a71-73ae-499d-8c15-faa9aef0c3f2',
        '49535343-fe7d-4ae5-8fa9-9fafd205e455', // ISSC Serial
        '0000af00-0000-1000-8000-00805f9b34fb',
        '0000e701-0000-1000-8000-00805f9b34fb',
        '0000ff00-0000-1000-8000-00805f9b34fb',
        '0000ff01-0000-1000-8000-00805f9b34fb'
    ];

    return {
        isBluetoothSupported: function () {
            return !!(navigator.bluetooth || navigator.serial);
        },

        getPairedDeviceName: function () {
            return localStorage.getItem("web_bt_printer_name") || "";
        },

        getPrinterSettings: function () {
            return {
                selectedKey: localStorage.getItem("senang_selected_printer_key") || "BT_58",
                pairedDeviceName: localStorage.getItem("web_bt_printer_name") || "",
                net58Ip: localStorage.getItem("senang_net58_ip") || "192.168.1.200",
                net80Ip: localStorage.getItem("senang_net80_ip") || "192.168.1.200"
            };
        },

        pairBluetoothDevice: async function () {
            let lastError = null;

            // 1. Try Web Bluetooth (BLE / GATT)
            if (navigator.bluetooth) {
                try {
                    let device = null;
                    try {
                        device = await navigator.bluetooth.requestDevice({
                            acceptAllDevices: true,
                            optionalServices: optionalServices
                        });
                    } catch (e1) {
                        console.warn("[webPrinter] acceptAllDevices failed, trying prefix filter...", e1);
                        device = await navigator.bluetooth.requestDevice({
                            filters: [
                                { namePrefix: 'Printer' },
                                { namePrefix: 'POS' },
                                { namePrefix: 'RPP' },
                                { namePrefix: 'MPT' },
                                { namePrefix: 'ZJ' },
                                { namePrefix: 'Goojprt' },
                                { namePrefix: 'Xprinter' },
                                { namePrefix: 'BT' },
                                { namePrefix: 'InnerPrinter' },
                                { namePrefix: '58' },
                                { namePrefix: '80' }
                            ],
                            optionalServices: optionalServices
                        });
                    }

                    if (device) {
                        activeBtDevice = device;
                        activeSerialPort = null;
                        const deviceName = device.name || "Bluetooth Printer (" + device.id.substring(0, 5) + ")";
                        localStorage.setItem("web_bt_printer_name", deviceName);
                        localStorage.setItem("web_bt_printer_id", device.id);
                        localStorage.setItem("web_printer_type", "bluetooth");
                        return { success: true, name: deviceName };
                    }
                } catch (err) {
                    console.warn("[webPrinter] Web Bluetooth pairing skipped or failed:", err);
                    lastError = err;
                }
            }

            // 2. Try Web Serial (Classic Bluetooth SPP / COM Ports / USB Printers)
            if (navigator.serial) {
                try {
                    const port = await navigator.serial.requestPort();
                    if (port) {
                        activeSerialPort = port;
                        activeBtDevice = null;
                        const info = port.getInfo();
                        const deviceName = info.usbVendorId ? `Serial Printer (USB:${info.usbVendorId})` : "Bluetooth SPP COM Printer";
                        localStorage.setItem("web_bt_printer_name", deviceName);
                        localStorage.setItem("web_printer_type", "serial");
                        return { success: true, name: deviceName };
                    }
                } catch (err) {
                    console.warn("[webPrinter] Web Serial pairing skipped or failed:", err);
                    if (!lastError) lastError = err;
                }
            }

            return {
                success: false,
                error: (lastError && lastError.message) ? lastError.message : "No printer device paired. Please ensure Bluetooth is ON."
            };
        },

        sendBluetoothEscPos: async function (base64Data) {
            try {
                const binaryString = atob(base64Data);
                const bytes = new Uint8Array(binaryString.length);
                for (let i = 0; i < binaryString.length; i++) {
                    bytes[i] = binaryString.charCodeAt(i);
                }

                const printerType = localStorage.getItem("web_printer_type") || "bluetooth";

                // ─── Web Serial path (Classic Bluetooth SPP / COM port) ──────
                if (printerType === "serial") {
                    if (!navigator.serial) return { success: false, error: "Web Serial not supported on this browser." };

                    // Restore already-granted port silently (no picker)
                    if (!activeSerialPort) {
                        const ports = await navigator.serial.getPorts();
                        if (ports && ports.length > 0) activeSerialPort = ports[0];
                    }

                    if (!activeSerialPort) {
                        return { success: false, error: "No paired Serial/COM printer found. Please pair a device first from the Printer menu." };
                    }

                    if (!activeSerialPort.readable || !activeSerialPort.writable) {
                        await activeSerialPort.open({ baudRate: 9600 });
                    }

                    const writer = activeSerialPort.writable.getWriter();
                    await writer.write(bytes);
                    writer.releaseLock();
                    return { success: true };
                }

                // ─── Web Bluetooth GATT path ─────────────────────────────────
                if (!navigator.bluetooth) {
                    return { success: false, error: "Web Bluetooth API not supported on this browser." };
                }

                // Silently restore previously granted device — no picker
                if (!activeBtDevice) {
                    const savedId = localStorage.getItem("web_bt_printer_id");
                    const devices = await navigator.bluetooth.getDevices();
                    if (savedId && devices.length > 0) {
                        activeBtDevice = devices.find(d => d.id === savedId);
                    }
                    if (!activeBtDevice && devices.length > 0) {
                        activeBtDevice = devices[0];
                    }
                }

                // If still no device, tell user to pair — do NOT open picker automatically
                if (!activeBtDevice) {
                    return { success: false, error: "No paired Bluetooth printer found. Please go to the Printer menu and tap 'Pair Bluetooth Device'." };
                }

                // Connect GATT
                if (!activeBtDevice.gatt.connected) {
                    activeGattServer = await activeBtDevice.gatt.connect();
                } else {
                    activeGattServer = activeBtDevice.gatt;
                }

                const services = await activeGattServer.getPrimaryServices();
                let writableChar = null;

                for (const service of services) {
                    try {
                        const characteristics = await service.getCharacteristics();
                        for (const char of characteristics) {
                            if (char.properties.write || char.properties.writeWithoutResponse) {
                                writableChar = char;
                                break;
                            }
                        }
                    } catch { }
                    if (writableChar) break;
                }

                if (!writableChar) {
                    return { success: false, error: "Connected to device, but no writable printer characteristic was found. Try pairing again." };
                }

                // Chunk-send ESC/POS bytes
                const chunkSize = 100;
                for (let i = 0; i < bytes.length; i += chunkSize) {
                    const chunk = bytes.slice(i, i + chunkSize);
                    if (writableChar.properties.writeWithoutResponse) {
                        await writableChar.writeValueWithoutResponse(chunk);
                    } else {
                        await writableChar.writeValue(chunk);
                    }
                    await new Promise(r => setTimeout(r, 25));
                }

                return { success: true };

            } catch (err) {
                console.error("[webPrinter] Send ESC/POS error:", err);
                // If GATT disconnected, clear device so next attempt can reconnect
                if (err.message && err.message.includes("GATT")) {
                    activeGattServer = null;
                    activeBtDevice = null;
                }
                return { success: false, error: err.message || "Failed to print over Bluetooth." };
            }
        },

        printHtmlReceipt: function (htmlContent, paperMm) {
            try {
                let printFrame = document.getElementById("thermal-print-iframe");
                if (!printFrame) {
                    printFrame = document.createElement("iframe");
                    printFrame.id = "thermal-print-iframe";
                    printFrame.style.position = "fixed";
                    printFrame.style.right = "0";
                    printFrame.style.bottom = "0";
                    printFrame.style.width = "0px";
                    printFrame.style.height = "0px";
                    printFrame.style.border = "none";
                    document.body.appendChild(printFrame);
                }

                const paperWidth = (paperMm === 80) ? "78mm" : "54mm";
                const doc = printFrame.contentWindow.document;
                doc.open();
                doc.write(`
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Receipt Print</title>
                        <style>
                            @page {
                                size: ${paperWidth} auto;
                                margin: 0;
                            }
                            body {
                                margin: 0;
                                padding: 4px;
                                width: ${paperWidth};
                                font-family: 'Courier New', monospace;
                                background: #fff;
                                color: #000;
                            }
                            @media print {
                                body { width: ${paperWidth}; }
                            }
                        </style>
                    </head>
                    <body>
                        ${htmlContent}
                        <script>
                            window.onload = function() {
                                window.focus();
                                window.print();
                            };
                        </script>
                    </body>
                    </html>
                `);
                doc.close();
                return { success: true };
            } catch (err) {
                console.error("[webPrinter] Print HTML error:", err);
                return { success: false, error: err.message || "Browser print failed." };
            }
        }
    };
})();

