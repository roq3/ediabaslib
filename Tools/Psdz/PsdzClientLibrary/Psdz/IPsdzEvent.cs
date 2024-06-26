﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BMW.Rheingold.Psdz.Model.Ecu;
using BMW.Rheingold.Psdz.Model.Localization;

namespace BMW.Rheingold.Psdz.Model.Events
{
    public interface IPsdzEvent : ILocalizableMessage
    {
        IPsdzEcuIdentifier EcuId { get; }

        string EventId { get; }

        string Message { get; }

        long Timestamp { get; }
    }
}
