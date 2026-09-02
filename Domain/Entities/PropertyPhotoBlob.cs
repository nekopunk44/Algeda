using Domain.Common;

namespace Domain.Entities
{
    public class PropertyPhotoBlob : BaseEntity
    {
        public string Path { get; private set; }

        public string ContentType { get; private set; }

        public byte[] Content { get; private set; }

        public string ContentHash { get; private set; }

        public int ContentLength { get; private set; }

        private PropertyPhotoBlob()
        {
            Path = string.Empty;
            ContentType = "application/octet-stream";
            Content = [];
            ContentHash = string.Empty;
        }

        public PropertyPhotoBlob(
            string path,
            string contentType,
            byte[] content,
            string contentHash)
        {
            Path = NormalizePath(path);
            ContentType = NormalizeContentType(contentType);
            Content = NormalizeContent(content);
            ContentHash = NormalizeHash(contentHash);
            ContentLength = Content.Length;
        }

        public void ReplaceContent(
            string contentType,
            byte[] content,
            string contentHash)
        {
            ContentType = NormalizeContentType(contentType);
            Content = NormalizeContent(content);
            ContentHash = NormalizeHash(contentHash);
            ContentLength = Content.Length;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new DomainException("Путь к фото не может быть пустым.");
            }

            var normalized = path.Trim().Replace("\\", "/");
            return normalized.StartsWith("/", StringComparison.Ordinal)
                ? normalized
                : "/" + normalized;
        }

        private static string NormalizeContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new DomainException("Тип контента фото не указан.");
            }

            return contentType.Trim().ToLowerInvariant();
        }

        private static byte[] NormalizeContent(byte[] content)
        {
            if (content is null || content.Length == 0)
            {
                throw new DomainException("Содержимое фото не может быть пустым.");
            }

            return content;
        }

        private static string NormalizeHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new DomainException("Хэш фото не может быть пустым.");
            }

            return hash.Trim().ToLowerInvariant();
        }
    }
}
