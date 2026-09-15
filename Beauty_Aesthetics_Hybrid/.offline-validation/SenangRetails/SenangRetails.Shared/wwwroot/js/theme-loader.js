(function () {
    window.applySavedTheme = function (themeData) {
        try {
            var t = themeData;
            if (!t) {
                var json = localStorage.getItem('senang_custom_theme');
                if (json) {
                    t = JSON.parse(json);
                }
            }
            if (!t) {
                t = {
                    Rank: "⭐",
                    PresetName: "Vibrant Orange",
                    PrimaryColor: "#F2600C",
                    HoverColor: "#EA580C",
                    ActiveColor: "#C2410C",
                    AccentColor: "#2563EB",
                    TextColor: "#1E293B",
                    SurfaceColor: "#FFF7ED",
                    BadgeColor: "#FFEDD5",
                    BorderColor: "#FED7AA",
                    TextOnPrimary: "#FFFFFF",
                    Character: "Default brand color - energetic & warm"
                };
            }
            if (t && t.PrimaryColor) {
                var r = 242, g = 96, b = 12;
                if (t.PrimaryColor.startsWith('#') && t.PrimaryColor.length === 7) {
                    r = parseInt(t.PrimaryColor.substring(1, 3), 16);
                    g = parseInt(t.PrimaryColor.substring(3, 5), 16);
                    b = parseInt(t.PrimaryColor.substring(5, 7), 16);
                }
                var rgbStr = `${r}, ${g}, ${b}`;
                var textOnPrimary = t.TextOnPrimary || '#FFFFFF';
                var accentColor = t.AccentColor || '#2563EB';
                var textColor = t.TextColor || '#1E293B';

                var css = `
                    :root {
                        --primary: ${t.PrimaryColor} !important;
                        --primary-rgb: ${rgbStr} !important;
                        --primary-dark: ${t.HoverColor} !important;
                        --primary-darker: ${t.ActiveColor} !important;
                        --primary-rose: ${t.PrimaryColor} !important;
                        --accent: ${accentColor} !important;
                        --color-accent: ${accentColor} !important;
                        --color-primary: ${t.PrimaryColor} !important;
                        --color-base-1: ${t.PrimaryColor} !important;
                        --color-base-2: ${t.BorderColor} !important;
                        --color-base-3: ${t.PrimaryColor} !important;
                        --color-base-4: ${t.SurfaceColor} !important;
                        --color-base-5: ${t.BadgeColor} !important;
                        --color-secondary: ${t.SurfaceColor} !important;
                        --color-surface: #FFFFFF !important;
                        --color-background: #FAFAFA !important;
                        --color-background-secondary: #FFFFFF !important;
                        --c1: ${t.PrimaryColor} !important;
                        --c2: ${t.HoverColor} !important;
                        --c3: ${t.SurfaceColor} !important;
                        --c4: ${t.BadgeColor} !important;
                        --c5: #FAFAFA !important;
                        --bg-body: #FAFAFA !important;
                        --bg-surface: #FAFAFA !important;
                        --bg-card: #FFFFFF !important;
                        --bg-surface-2: ${t.BadgeColor} !important;
                        --beige-bg: ${t.SurfaceColor} !important;
                        --pink-light: ${t.SurfaceColor} !important;
                        --off-white-bg: #EDEAE3 !important;
                        --border: #EEEEEE !important;
                        --color-border: #EEEEEE !important;
                        --color-border-focus: ${t.PrimaryColor} !important;
                        --numpad-bg: ${t.SurfaceColor} !important;
                        --numpad-hover: ${t.BadgeColor} !important;
                        --text-primary: ${textColor} !important;
                        --color-text: ${textColor} !important;
                        --text-secondary: ${textOnPrimary} !important;
                        --text-on-primary: ${textOnPrimary} !important;
                        --shadow: 0 4px 12px rgba(0, 0, 0, 0.08) !important;
                    }

                    /* All Buttons on All Pages (including Login, Checkout, POS, Modals) */
                    .btn-primary, .add-btn, .add-btn-sm, .save-btn, .btn-save, .pg-add-btn, .pg-add-btn-sm,
                    .pg-btn-save, .kp-submit, .submit-btn, .primary-btn, .btn-brand,
                    .checkout-btn, .action-btn-primary, .confirm-btn, .payment-btn, .sign-in-btn,
                    .orders-btn-primary, .cts-btn-primary, .add-payment-btn {
                        background-color: ${t.PrimaryColor} !important;
                        color: ${textOnPrimary} !important;
                        border-color: ${t.PrimaryColor} !important;
                        transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1) !important;
                    }

                    /* All Button Hover Effects */
                    .btn-primary:hover:not(:disabled), .add-btn:hover:not(:disabled), .add-btn-sm:hover:not(:disabled),
                    .save-btn:hover:not(:disabled), .btn-save:hover:not(:disabled),
                    .pg-add-btn:hover:not(:disabled), .pg-add-btn-sm:hover:not(:disabled), .pg-btn-save:hover:not(:disabled),
                    .kp-submit:hover:not(:disabled), .submit-btn:hover:not(:disabled), .primary-btn:hover:not(:disabled),
                    .btn-brand:hover:not(:disabled), .checkout-btn:hover:not(:disabled), .action-btn-primary:hover:not(:disabled),
                    .confirm-btn:hover:not(:disabled), .payment-btn:hover:not(:disabled), .sign-in-btn:hover:not(:disabled),
                    .orders-btn-primary:hover:not(:disabled), .cts-btn-primary:hover:not(:disabled),
                    .add-payment-btn:hover:not(:disabled) {
                        background-color: ${t.HoverColor} !important;
                        border-color: ${t.HoverColor} !important;
                        color: ${textOnPrimary} !important;
                        transform: translateY(-1px) !important;
                        box-shadow: 0 4px 14px rgba(${rgbStr}, 0.3) !important;
                    }

                    /* All Button Active / Pressed Effects */
                    .btn-primary:active:not(:disabled), .add-btn:active:not(:disabled), .save-btn:active:not(:disabled),
                    .btn-save:active:not(:disabled), .pg-btn-save:active:not(:disabled), .kp-submit:active:not(:disabled),
                    .submit-btn:active:not(:disabled), .checkout-btn:active:not(:disabled), .action-btn-primary:active:not(:disabled),
                    .confirm-btn:active:not(:disabled), .payment-btn:active:not(:disabled), .sign-in-btn:active:not(:disabled),
                    .orders-btn-primary:active:not(:disabled), .cts-btn-primary:active:not(:disabled) {
                        background-color: ${t.ActiveColor} !important;
                        border-color: ${t.ActiveColor} !important;
                        transform: translateY(0) !important;
                        box-shadow: 0 2px 6px rgba(${rgbStr}, 0.2) !important;
                    }

                    /* Interactive Cards & Items Hover States (SenangApp Design) */
                    .service-card:hover, .customer-list-item:hover, .order-item:hover,
                    .orders-btn-secondary:hover, .branch-card:hover, .tab:hover:not(.active),
                    .orders-btn-tertiary:hover {
                        background-color: ${t.SurfaceColor} !important;
                    }

                    /* Accent Highlights & Badges */
                    .accent-tag, .badge-accent, .highlight-accent {
                        background-color: ${accentColor} !important;
                        color: #FFFFFF !important;
                    }

                    /* Active Navigation & Tabs */
                    .category-item.active, .nav-link.active, .tab-item.active, .nav-pills .nav-link.active,
                    .sidebar-item.active, .tab-btn.active, .tab.active, .type-tab.active {
                        background-color: ${t.PrimaryColor} !important;
                        color: ${textOnPrimary} !important;
                        border-color: ${t.PrimaryColor} !important;
                    }
                    .category-item.active i, .category-item.active span,
                    .nav-link.active i, .tab-item.active i, .tab.active span {
                        color: ${textOnPrimary} !important;
                    }

                    /* Inputs Focus */
                    input:focus, select:focus, textarea:focus, .form-control:focus, .form-input:focus, .pg-input:focus {
                        border-color: ${t.PrimaryColor} !important;
                        box-shadow: 0 0 0 3px ${t.BadgeColor} !important;
                    }

                    /* Checkboxes & Radios */
                    input[type="checkbox"], input[type="radio"] {
                        accent-color: ${t.PrimaryColor} !important;
                    }
                    input[type="checkbox"].custom-checkbox:checked {
                        background-color: ${t.PrimaryColor} !important;
                        border-color: ${t.PrimaryColor} !important;
                    }
                `;
                var styleTag = document.getElementById('senang-dynamic-theme-style');
                if (!styleTag) {
                    styleTag = document.createElement('style');
                    styleTag.id = 'senang-dynamic-theme-style';
                    document.head.appendChild(styleTag);
                }
                styleTag.innerHTML = css;
            }
        } catch (e) {
            console.error('Failed to load dynamic theme early:', e);
        }
    };

    // Run immediately when script is parsed
    window.applySavedTheme();
})();
