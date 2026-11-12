using KleeneStar.Core.WebTheme;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The base of the endpoints behind the appearance form of the core's and the portal's
    /// profile (one sealed endpoint per application, because an endpoint belongs to one):
    /// reads and writes the adjustments the caller lays over the theme - accent color, font
    /// size, density, motion - for the form on the profile's appearance page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Confined to the caller like the rest of the profile: whatever id the form sends, it is
    /// the caller's record that is read and changed, and a caller who is not signed in has
    /// none. Theme and mode are not part of it - each has a selector that stores at once
    /// (<see cref="RestApiAppearanceTheme"/>, <see cref="RestApiAppearanceScheme"/>) - so a save here keeps them as they are.
    /// </para>
    /// <para>
    /// The form item is <see cref="AppearanceForm"/>, not the stored record: the form speaks in
    /// its own answers (<em>theme accent</em> or <em>custom</em>, 100 % rather than nothing),
    /// and every answer is reduced again by <see cref="AppearanceOptions"/> before it is kept,
    /// because each one ends up in a stylesheet.
    /// </para>
    /// </remarks>
    public abstract class RestApiAppearance : RestApiCrud<AppearanceForm>
    {
        /// <summary>
        /// The field choosing between the theme's accent and a custom one.
        /// </summary>
        public const string AccentModeField = nameof(AppearanceForm.AccentMode);

        /// <summary>
        /// The field carrying the custom accent color.
        /// </summary>
        public const string AccentColorField = nameof(AppearanceForm.AccentColor);

        /// <summary>
        /// The field carrying the font size in percent.
        /// </summary>
        public const string FontScaleField = nameof(AppearanceForm.FontScale);

        /// <summary>
        /// The field carrying the density.
        /// </summary>
        public const string DensityField = nameof(AppearanceForm.Density);

        /// <summary>
        /// The field carrying the motion choice.
        /// </summary>
        public const string ReduceMotionField = nameof(AppearanceForm.ReduceMotion);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        protected RestApiAppearance()
        {
        }

        /// <summary>
        /// Refuses a create: the record exists for every signed-in caller.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return new ResponseForbidden();
        }

        /// <summary>
        /// Clears the caller's adjustments - the "reset" of the form - and keeps theme and mode.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return CoreHub.AppearanceManager.Update(request, x =>
            {
                x.AccentColor = null;
                x.FontScale = null;
                x.Density = null;
                x.ReduceMotion = false;
            })
                ? new ResponseNoContent()
                : new ResponseForbidden();
        }

        /// <summary>
        /// Retrieves the caller's adjustments as the form's answers, whatever the query names.
        /// </summary>
        /// <param name="query">The query (ignored - there is one record per caller).</param>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The current request.</param>
        /// <returns>The caller's record, or nothing for a caller who is not signed in.</returns>
        protected override IEnumerable<AppearanceForm> Retrieve(IQuery<AppearanceForm> query, IQueryContext context, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return [];
            }

            return [AppearanceForm.From(identityId, CoreHub.AppearanceManager.GetCurrent(request))];
        }

        /// <summary>
        /// Validates the answers the payload carries - and only those: a field the payload does
        /// not name is not being changed.
        /// </summary>
        /// <remarks>
        /// The endpoint answers <c>PUT</c> and <c>PATCH</c> alike (the framework's
        /// <see cref="RestApiCrud{TIndexItem}.Update(IRequest)"/>), so a partial payload is a
        /// legitimate request. A value that is sent but not offered is refused rather than read
        /// as "no choice", which would silently reset what the caller had.
        /// </remarks>
        /// <param name="existingItem">The caller's record.</param>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="request">The current request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult Validate(AppearanceForm existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);

            if (existingItem is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.signedout"), AccentModeField);

                return result;
            }

            var hasMode = TryRead(payload, AccentModeField, out var mode);
            var hasColor = TryRead(payload, AccentColorField, out var color);

            if (hasMode && !IsMode(mode, AppearanceOptions.AccentCustom) && !IsMode(mode, AppearanceOptions.AccentTheme))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.unoffered"), AccentModeField);
            }
            else if (hasMode && IsMode(mode, AppearanceOptions.AccentCustom))
            {
                // a custom accent needs a color: the one sent, else the one already kept
                var accent = hasColor
                    ? AppearanceOptions.NormalizeAccent(color)
                    : CoreHub.AppearanceManager.GetCurrent(request).AccentColor;

                if (accent is null)
                {
                    result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.accent.invalid"), AccentColorField);
                }
            }
            else if (!hasMode && hasColor && !string.IsNullOrWhiteSpace(color) && AppearanceOptions.NormalizeAccent(color) is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.accent.invalid"), AccentColorField);
            }

            if (TryRead(payload, FontScaleField, out var scale) && !IsOfferedFontScale(scale))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.unoffered"), FontScaleField);
            }

            if (TryRead(payload, DensityField, out var density) && !IsOfferedDensity(density))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.unoffered"), DensityField);
            }

            if (TryRead(payload, ReduceMotionField, out var motion) && !IsFlag(motion))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:profile.appearance.unoffered"), ReduceMotionField);
            }

            return result;
        }

        /// <summary>
        /// Stores the adjustments the payload carries and keeps every other one.
        /// </summary>
        /// <remarks>
        /// The change is applied by <see cref="WebManager.IAppearanceManager.Update"/> to the
        /// record it reads inside its gate, so a field this request does not name keeps what is
        /// stored at that moment - including a change another request made since the form was
        /// loaded.
        /// </remarks>
        /// <param name="existingItem">The caller's record.</param>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="request">The current request.</param>
        /// <returns>The result of the update.</returns>
        protected override IRestApiCrudResultUpdate Update(AppearanceForm existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var hasMode = TryRead(payload, AccentModeField, out var mode);
            var hasColor = TryRead(payload, AccentColorField, out var color);
            var hasScale = TryRead(payload, FontScaleField, out var scale);
            var hasDensity = TryRead(payload, DensityField, out var density);
            var hasMotion = TryRead(payload, ReduceMotionField, out var motion);

            var stored = CoreHub.AppearanceManager.Update(request, x =>
            {
                if (hasMode)
                {
                    // the theme's accent clears the custom one; a custom accent takes the color
                    // sent with it, else keeps the one already kept
                    x.AccentColor = IsMode(mode, AppearanceOptions.AccentCustom)
                        ? (hasColor ? AppearanceOptions.NormalizeAccent(color) : x.AccentColor)
                        : null;
                }
                else if (hasColor)
                {
                    // a color alone sets (or, blank, clears) the custom accent
                    x.AccentColor = AppearanceOptions.NormalizeAccent(color);
                }

                if (hasScale)
                {
                    x.FontScale = AppearanceOptions.NormalizeFontScale(scale);
                }

                if (hasDensity)
                {
                    x.Density = AppearanceOptions.NormalizeDensity(density);
                }

                if (hasMotion)
                {
                    x.ReduceMotion = AppearanceOptions.ReadFlag(motion);
                }
            });

            if (!stored)
            {
                // validation found the caller a moment ago; a session that ended in between
                // is reported as a failed request rather than a success that stored nothing
                throw new InvalidOperationException("The appearance was not stored: nobody is signed in.");
            }

            return new RestApiCrudResultUpdate
            {
                Message = I18N.Translate(request, "kleenestar.core:profile.appearance.saved")
            };
        }

        /// <summary>
        /// Reads a field the payload carries.
        /// </summary>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">Its value as text; empty for a field sent without one.</param>
        /// <returns>Whether the payload names the field at all.</returns>
        internal static bool TryRead(RestApiCrudFormData payload, string field, out string value)
        {
            value = null;

            if (payload is null || !payload.TryGetValue(field, out var raw))
            {
                return false;
            }

            value = raw?.ToString() ?? string.Empty;

            return true;
        }

        /// <summary>
        /// Tells whether an accent answer is the given one.
        /// </summary>
        /// <param name="value">The answer.</param>
        /// <param name="mode">The mode compared with.</param>
        /// <returns>Whether they match.</returns>
        private static bool IsMode(string value, string mode)
        {
            return string.Equals(value?.Trim(), mode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tells whether a font size answer is one of the offered sizes (100 included).
        /// </summary>
        /// <param name="value">The answer.</param>
        /// <returns>Whether it is offered.</returns>
        private static bool IsOfferedFontScale(string value)
        {
            return int.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale)
                && AppearanceOptions.FontScales.Contains(scale);
        }

        /// <summary>
        /// Tells whether a density answer is one of the offered densities.
        /// </summary>
        /// <param name="value">The answer.</param>
        /// <returns>Whether it is offered.</returns>
        private static bool IsOfferedDensity(string value)
        {
            return IsMode(value, AppearanceOptions.Comfortable) || IsMode(value, AppearanceOptions.Compact);
        }

        /// <summary>
        /// Tells whether a motion answer reads as a flag.
        /// </summary>
        /// <param name="value">The answer.</param>
        /// <returns>Whether it is a flag.</returns>
        private static bool IsFlag(string value)
        {
            return value?.Trim().ToLowerInvariant() is "true" or "false" or "on" or "off" or "yes" or "no" or "1" or "0";
        }
    }
}
