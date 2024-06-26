﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using BMW.Rheingold.Psdz.Model.Sfa.LocalizableMessageTo;

namespace BMW.Rheingold.Psdz.Model.Sfa
{
    [KnownType(typeof(PsdzLocalizableMessageTo))]
    [DataContract]
    public class PsdzSecurityBackendRequestFailureCto : IPsdzSecurityBackendRequestFailureCto
    {
        [DataMember]
        public ILocalizableMessageTo Cause { get; set; }

        [DataMember]
        public int Retry { get; set; }

        [DataMember]
        public string Url { get; set; }
    }
}
