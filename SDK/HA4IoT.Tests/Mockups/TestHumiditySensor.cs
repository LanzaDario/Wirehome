﻿using System;
using HA4IoT.Contracts.Services.Settings;
using HA4IoT.Sensors.HumiditySensors;

namespace HA4IoT.Tests.Mockups
{
    public class TestHumiditySensor : HumiditySensor
    {
        public TestHumiditySensor(string id, ISettingsService settingsService, TestSensorAdapter endpoint) 
            : base(id, settingsService, endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            Endpoint = endpoint;
        }

        public TestSensorAdapter Endpoint{ get; }
    }
}
