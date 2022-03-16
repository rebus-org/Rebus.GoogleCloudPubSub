using System;
using System.IO;
using Newtonsoft.Json;

namespace Rebus.GoogleCloudPubSub
{
    public class GoogleCredentials
    {
        [JsonProperty("project_id")] public string ProjectId { get; set; }

        public static GoogleCredentials GetGoogleCredentialsFromEnvironmentVariable()
        {
            var emulatorHost = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST");
            var projectId = Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID");
            var configFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (!string.IsNullOrEmpty(emulatorHost) && !string.IsNullOrEmpty(projectId))
            {
                return new GoogleCredentials() { ProjectId = projectId };
            }

            if (string.IsNullOrEmpty(configFilePath))
            {
                throw new ArgumentException(
                    $"Please ensure that tests are running with an environment variable named 'GOOGLE_APPLICATION_CREDENTIALS' that points to a JSON file containing appropriate Google credentials");
            }

            if (!File.Exists(configFilePath))
            {
                throw new ArgumentException(
                    $"Could not find any GOOGLE_APPLICATION_CREDENTIALS on path {configFilePath}");
            }

            return JsonConvert.DeserializeObject<GoogleCredentials>(File.ReadAllText(configFilePath));
        }
    }
}