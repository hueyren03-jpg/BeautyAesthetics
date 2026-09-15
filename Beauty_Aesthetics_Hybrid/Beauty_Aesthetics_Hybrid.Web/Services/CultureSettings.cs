using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using System;
using System.Globalization;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_Hybrid.Web.Services
{
    public class CultureSettings : ICultureSettings
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CultureSettings(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetCulture()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                var feature = context.Features.Get<IRequestCultureFeature>();
                if (feature != null)
                {
                    return feature.RequestCulture.Culture.Name;
                }
            }
            return CultureInfo.CurrentCulture.Name;
        }

        public void SetCulture(string culture)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
                );
            }
        }
    }
}
