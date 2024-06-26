﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using BMW.Rheingold.Psdz.Model.Localization;

namespace BMW.Rheingold.Psdz.Model.Tal.TalStatus
{
    [KnownType(typeof(PsdzTalElement))]
    [DataContract]
    public class PsdzFailureCause : IPsdzFailureCause, ILocalizableMessage
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string IdReference { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public int MessageId { get; set; }

        [DataMember]
        public IPsdzTalElement TalElement { get; set; }

        [DataMember]
        public long Timestamp { get; set; }
    }
}
