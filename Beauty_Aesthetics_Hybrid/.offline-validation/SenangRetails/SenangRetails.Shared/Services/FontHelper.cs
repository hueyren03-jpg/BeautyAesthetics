using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using QuestPDF.Infrastructure;

namespace SenangRetails.Shared.Services
{
    public static class FontHelper
    {
        private static byte[] _regularFontBytes;
        private static byte[] _boldFontBytes;
        private static bool _isRegistered = false;
        private static readonly object _lock = new object();

        public static void RegisterFonts()
        {
            if (_isRegistered)
                return;

            lock (_lock)
            {
                if (_isRegistered)
                    return;

                try
                {
                    // Try to load from MAUI embedded resources first
                    LoadFontsFromAssembly(Assembly.GetExecutingAssembly());

                    // If not found, try to load from Shared assembly
                    if (_regularFontBytes == null)
                    {
                        var sharedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.FullName?.Contains("SenangRetails.Shared") == true);

                        if (sharedAssembly != null)
                        {
                            LoadFontsFromAssembly(sharedAssembly);
                        }
                    }

                    // If still not found, try to load from file system
                    if (_regularFontBytes == null)
                    {
                        LoadFontsFromFileSystem();
                    }

                    // Register with QuestPDF
                    if (_regularFontBytes != null)
                    {
                        RegisterFontWithQuestPDF("NotoSansSC", _regularFontBytes);
                        if (_boldFontBytes != null)
                        {
                            RegisterFontWithQuestPDF("NotoSansSC-Bold", _boldFontBytes);
                        }
                        _isRegistered = true;
                        Console.WriteLine("✅ Fonts registered successfully with QuestPDF");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ No fonts found to register");
                        _isRegistered = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error registering fonts: {ex.Message}");
                    _isRegistered = true;
                }
            }
        }

        private static void LoadFontsFromAssembly(Assembly assembly)
        {
            try
            {
                var resourceNames = assembly.GetManifestResourceNames();

                // Find regular font
                var regularResource = resourceNames.FirstOrDefault(r =>
                    r.Contains("NotoSansSC") &&
                    r.Contains("Regular") &&
                    (r.EndsWith(".ttf") || r.EndsWith(".otf")));

                // Find bold font
                var boldResource = resourceNames.FirstOrDefault(r =>
                    r.Contains("NotoSansSC") &&
                    r.Contains("Bold") &&
                    (r.EndsWith(".ttf") || r.EndsWith(".otf")));

                // If not found, try without Regular/Bold in name
                if (regularResource == null)
                {
                    regularResource = resourceNames.FirstOrDefault(r =>
                        r.Contains("NotoSansSC") &&
                        (r.EndsWith(".ttf") || r.EndsWith(".otf")));
                }

                if (regularResource != null)
                {
                    using var stream = assembly.GetManifestResourceStream(regularResource);
                    if (stream != null)
                    {
                        using var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                        _regularFontBytes = memoryStream.ToArray();
                        Console.WriteLine($"✅ Regular font loaded from: {regularResource} ({_regularFontBytes.Length} bytes)");
                    }
                }

                if (boldResource != null)
                {
                    using var stream = assembly.GetManifestResourceStream(boldResource);
                    if (stream != null)
                    {
                        using var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                        _boldFontBytes = memoryStream.ToArray();
                        Console.WriteLine($"✅ Bold font loaded from: {boldResource} ({_boldFontBytes.Length} bytes)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading fonts from assembly: {ex.Message}");
            }
        }

        private static void LoadFontsFromFileSystem()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var fontPaths = new[]
                {
                    Path.Combine(basePath, "Resources", "Fonts", "NotoSansSC-Regular.ttf"),
                    Path.Combine(basePath, "Fonts", "NotoSansSC-Regular.ttf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fonts", "NotoSansSC-Regular.ttf"),
                };

                foreach (var fontPath in fontPaths)
                {
                    if (File.Exists(fontPath))
                    {
                        _regularFontBytes = File.ReadAllBytes(fontPath);
                        Console.WriteLine($"✅ Regular font loaded from file: {fontPath}");
                        break;
                    }
                }

                var boldPaths = fontPaths.Select(p => p.Replace("Regular", "Bold")).ToArray();
                foreach (var fontPath in boldPaths)
                {
                    if (File.Exists(fontPath))
                    {
                        _boldFontBytes = File.ReadAllBytes(fontPath);
                        Console.WriteLine($"✅ Bold font loaded from file: {fontPath}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading fonts from file system: {ex.Message}");
            }
        }

        private static void RegisterFontWithQuestPDF(string fontName, byte[] fontData)
        {
            try
            {
                using var stream = new MemoryStream(fontData);

                // Try multiple registration methods for QuestPDF 2023.12.6
                try
                {
                    // Method 1: Try RegisterFontType
                    var method = typeof(QuestPDF.Drawing.FontManager)
                        .GetMethod("RegisterFontType", new[] { typeof(Stream) });

                    if (method != null)
                    {
                        method.Invoke(null, new object[] { stream });
                        Console.WriteLine($"✅ Font '{fontName}' registered via RegisterFontType");
                        return;
                    }
                }
                catch { }

                try
                {
                    // Method 2: Try RegisterFont
                    var method = typeof(QuestPDF.Drawing.FontManager)
                        .GetMethod("RegisterFont", new[] { typeof(string), typeof(byte[]) });

                    if (method != null)
                    {
                        method.Invoke(null, new object[] { fontName, fontData });
                        Console.WriteLine($"✅ Font '{fontName}' registered via RegisterFont");
                        return;
                    }
                }
                catch { }

                try
                {
                    // Method 3: Try AddFont
                    var method = typeof(QuestPDF.Drawing.FontManager)
                        .GetMethod("AddFont", new[] { typeof(string), typeof(byte[]) });

                    if (method != null)
                    {
                        method.Invoke(null, new object[] { fontName, fontData });
                        Console.WriteLine($"✅ Font '{fontName}' registered via AddFont");
                        return;
                    }
                }
                catch { }

                Console.WriteLine($"⚠️ Could not register font '{fontName}' with QuestPDF. Font will be used by name.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error registering font '{fontName}': {ex.Message}");
            }
        }

        public static byte[] GetRegularFont()
        {
            RegisterFonts();
            return _regularFontBytes;
        }

        public static byte[] GetBoldFont()
        {
            RegisterFonts();
            return _boldFontBytes;
        }
    }
}