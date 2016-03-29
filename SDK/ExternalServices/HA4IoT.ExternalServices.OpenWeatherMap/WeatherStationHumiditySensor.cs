﻿using System;
using HA4IoT.Actuators;
using HA4IoT.Contracts.Actuators;
using HA4IoT.Contracts.Sensors;

namespace HA4IoT.ExternalServices.OpenWeatherMap
{
    public class WeatherStationHumiditySensor : SingleValueSensorBase, IHumiditySensor
    {
        public WeatherStationHumiditySensor(ActuatorId id) 
            : base(id)
        {
        }

        public void SetValue(double value)
        {
            SetValueInternal(Convert.ToSingle(value));
        }
    }
}
