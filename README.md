# Rebus.GoogleCloudPubSub

[![install from nuget](https://img.shields.io/nuget/v/Rebus.GoogleCloudPubSub.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.GoogleCloudPubSub)


Provides a Google Cloud Pub/Sub-based transport implementation for [Rebus](https://github.com/rebus-org/Rebus).

It's just

```csharp
// ensure that the GOOGLE_APPLICATION_CREDENTIALS 
// environment variable has been set, and then:

Configure.With(...)
	.Transport(t => t.UsePubSub("your_queue"))
	.(...)
	.Start();
```

or

```csharp
// ensure that the GOOGLE_APPLICATION_CREDENTIALS 
// environment variable has been set, and then:

var bus = Configure.With(...)
	.Transport(t => t.UsePubSubAsOneWayClient())
	.(...)
	.Start();
```

and off you go! :rocket:


## Development and testing
Do the changes you please and make sure all tests run with success. Most tests have a dependency on GoogleCloudPubSub. A dependency which can be resolved either by having a pub-sub project in Google Cloud, [or by running a local emulator](https://cloud.google.com/pubsub/docs/emulator).

### Run tests using docker-compose and a local emulator
The docker-compose setup takes care of all necessities and outputs the test results to screen and a to current directory in JUnit Xml format. 
```
docker-compose build
docker-compose up
```
Expected output:
```
tests_1   | JunitXML Logger - Results File: /tmp/testresults.run/Rebus.GoogleCloudPubSub.Tests-test-result.xml
tests_1   |
tests_1   | Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 14 m 54 s - /app/Rebus.GoogleCloudPubSub.Tests/bin/Debug/net6.0/Rebus.GoogleCloudPubSub.Tests.dll (net6.0)
rebusgooglecloudpubsub_tests_1 exited with code 0
```

### Run tests using a local emulator
Run the pubsub emulator as you please. Make sure to put appropriate values into the environment variables before running the tests:
- `PUBSUB_EMULATOR_HOST` (*the address the emulator is accessible at*)
- `PUBSUB_PROJECT_ID` (*the project id the emulator is configured with*)

### Run tests using GooglePubSub cloud service
Make sure the environment variable `GOOGLE_APPLICATION_CREDENTIALS` points at your local [google-application-credentials](https://cloud.google.com/pubsub/docs/create-topic-client-libraries#before-you-begin) file. 

(*If both `PUBSUB_EMULATOR_HOST` and `PUBSUB_PROJECT_ID` environment variables are set, setting the `GOOGLE_APPLICATION_CREDENTIALS` will have no effect*)

![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---


