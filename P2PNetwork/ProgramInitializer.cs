using NSec.Cryptography;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace P2PNetwork
{
    public static class ProgramInitializer
    {
        public static string PublicKey { get; private set; }

        private static Key _privateKey;
        private static SignatureAlgorithm _algorithm = SignatureAlgorithm.Ed25519;

        public static void Initialization()
        {
            const string configPath = "appsettings.json";

            if (!File.Exists(configPath))
                throw new Exception("Program initialization error");

            string jsonContent = File.ReadAllText(configPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);

            // Пытаемся получить NodeId
            string? nodeId = doc.RootElement
                .GetProperty("Network")
                .GetProperty("NodeId")
                .GetString();

            if (!string.IsNullOrWhiteSpace(nodeId) || !string.IsNullOrWhiteSpace(PublicKey))
                return;// throw new Exception("Program initialization error");

            var data = Generate();
            PublicKey = data.PublicKey;

            // Создаем изменяемый объект
            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
            var network = JsonSerializer.Deserialize<Dictionary<string, object>>(
                jsonObject?["Network"]?.ToString() ?? "{}");

            if (network == null)
                throw new Exception("Program initialization error");

            network["NodeId"] = data.NodeId;
            jsonObject["Network"] = network;

            string updatedJson = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(configPath, updatedJson);
        }

        public static string GetSignature(string data)
        {
            if (_privateKey == null)
                throw new InvalidOperationException("Key not initialized. Call InitializeKey first.");

            // Конвертируем строку в байты
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            // Создаем подпись
            byte[] signature = _algorithm.Sign(_privateKey, dataBytes);

            // Возвращаем подпись в Base64
            return Convert.ToBase64String(signature);
        }

        private static (string NodeId, string PublicKey) Generate()
        {
            // Генерируем Ed25519 ключи
            var privateKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            // В реальности используйте NSec или libsodium для Ed25519

            _privateKey = Key.Import(_algorithm, privateKey, KeyBlobFormat.RawPrivateKey);

            var publicKey = DerivePublicKey(privateKey); // 32 байта

            // Хешируем публичный ключ для получения NodeId
            var hash = SHA256.HashData(publicKey);

            // Берём нужное количество бит (обычно 160 или 256)
            var nodeId = Convert.ToHexString(hash)[..40]; // 160 бит = 40 hex символов

            return (
                NodeId: nodeId,
                PublicKey: Convert.ToBase64String(publicKey)
            );
        }

        private static byte[] DerivePublicKey(byte[] privateKey)
        {
            var algorithm = _algorithm;
            var key = Key.Import(algorithm, privateKey, KeyBlobFormat.RawPrivateKey);
            var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            return publicKey;
        }
    }
}
