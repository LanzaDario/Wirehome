﻿using System.Collections.Generic;
using HA4IoT.Contracts.Actuators;
using HA4IoT.Contracts.Areas;
using HA4IoT.Contracts.Configuration;

namespace HA4IoT.Contracts.Core
{
    public interface IAreaController
    {
        void AddArea(IArea room);

        IArea GetArea(AreaId id);

        IList<IArea> GetAreas();
    }
}
