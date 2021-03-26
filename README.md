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

![](https://raw.githubusercontent.com/rebus-org/Rebus/master/artwork/little_rebusbus2_copy-200x200.png)

---


