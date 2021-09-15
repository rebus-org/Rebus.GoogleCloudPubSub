using System;
using System.IO;
using Newtonsoft.Json;

namespace Rebus.GoogleCloudPubSub
{
    public class GoogleCredentials
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("project_id")]
        public string ProjectId { get; set; }
        [JsonProperty("private_key_id")]
        public string PrivateKeyId { get; set; }
        [JsonProperty("private_key")]
        public string PrivateKey { get; set; }
        [JsonProperty("client_email")]
        public string ClientEmail { get; set; }
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        [JsonProperty("auth_uri")]
        public string AuthUri { get; set; }
        [JsonProperty("token_uri")]
        public string TokenUri { get; set; }
        [JsonProperty("auth_provider_x509_cert_url")]
        public string AuthProviderX509CertUrl { get; set; }
        [JsonProperty("client_x509_cert_url")]
        public string ClientX509CertUrl { get; set; }

        public static GoogleCredentials GetGoogleCredentialsFromEnvironmentVariable()
        {
            var configFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

            if (string.IsNullOrEmpty(configFilePath))
            {
                throw new ArgumentException($"Please ensure that tests are running with an environment variable named 'GOOGLE_APPLICATION_CREDENTIALS' that points to a JSON file containing appropriate Google credentials");
            }

            if (!File.Exists(configFilePath))
            {
                throw new ArgumentException($"Could not find any GOOGLE_APPLICATION_CREDENTIALS on path {configFilePath}");
            }

            return JsonConvert.DeserializeObject<GoogleCredentials>(File.ReadAllText(configFilePath));
        }
    }

}