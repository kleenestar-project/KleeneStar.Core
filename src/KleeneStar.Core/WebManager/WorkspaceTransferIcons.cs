using KleeneStar.Core.WebRestApi;
using System;
using System.IO;
using System.Security.Cryptography;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Transfers stored icons as inline images and restores them under immutable local addresses.
    /// </summary>
    public sealed class WorkspaceTransferIcons
    {
        private readonly string _directory;
        private readonly string _uri;

        /// <summary>
        /// Initializes the icon transfer for an installation.
        /// </summary>
        /// <param name="directory">The directory containing stored icons.</param>
        /// <param name="uri">The public address of the icon directory.</param>
        public WorkspaceTransferIcons(string directory, string uri)
        {
            _directory = Path.GetFullPath(directory);
            _uri = uri.TrimEnd('/') + "/";
        }

        /// <summary>
        /// Embeds a stored icon while preserving built-in, external and already inline addresses.
        /// </summary>
        /// <param name="icon">The icon to export.</param>
        /// <returns>The portable icon.</returns>
        public ImageIcon Export(ImageIcon icon)
        {
            var address = icon?.Uri?.ToString();

            if (address is null || !address.StartsWith(_uri, StringComparison.Ordinal))
            {
                return icon;
            }

            var name = address[_uri.Length..];

            // only direct files beneath the configured icon directory are eligible
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(['/', '\\', ':', '%', '?', '#']) >= 0
                || name != Path.GetFileName(name))
            {
                return icon;
            }

            var path = Path.Combine(_directory, name);
            var mime = Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".ico" => "image/x-icon",
                ".tiff" => "image/tiff",
                _ => null
            };

            return mime is not null && File.Exists(path)
                ? ImageIcon.FromString($"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}")
                : icon;
        }

        /// <summary>
        /// Restores an inline image without replacing or deleting an existing icon.
        /// </summary>
        /// <param name="icon">The icon read from the archive.</param>
        /// <returns>The local icon, or the unchanged address of a non-inline icon.</returns>
        public ImageIcon Import(ImageIcon icon)
        {
            var address = icon?.Uri?.ToString();

            if (address is null || !address.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return icon;
            }

            var options = AvatarStorage.Options;
            options.Formats["image/bmp"] = ".bmp";
            options.Formats["image/x-icon"] = ".ico";
            options.Formats["image/tiff"] = ".tiff";

            var field = new RestApiCrudFormData { ["Icon"] = address }
                .DetachAvatar("Icon", options, new RestApiValidationResult());

            if (field.State != RestApiFormFieldState.Valid)
            {
                throw new InvalidOperationException("The archive contains an invalid icon image.");
            }

            var image = field.Value;
            var name = "transfer-" + Convert.ToHexStringLower(SHA256.HashData(image.Content)) + image.Extension;
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, name);

            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, image.Content);
            }

            return ImageIcon.FromString(_uri + name);
        }
    }
}
