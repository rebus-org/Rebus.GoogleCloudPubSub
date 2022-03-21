using System;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace Rebus.GoogleCloudPubSub.Tests
{
    public class GoogleCredentials
    {
        [JsonProperty("project_id")] public string ProjectId { get; set; }

        public static string GetProjectIdFromGoogleCredentials()
        {
            var emulatorHost = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST");
            var projectId = Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID");
            var configFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            
            if (!string.IsNullOrEmpty(emulatorHost) && !string.IsNullOrEmpty(projectId))
            {
                return projectId ;
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

            return JsonConvert.DeserializeObject<GoogleCredentials>(File.ReadAllText(configFilePath)).ProjectId;
        }
    }
}