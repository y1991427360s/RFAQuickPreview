using System.Runtime.Serialization;

namespace RFAQuickPreview.Models
{
    [DataContract]
    public class FamilyParameterInfo
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public string StorageType { get; set; }

        [DataMember(Order = 3)]
        public string Value { get; set; }

        [DataMember(Order = 4)]
        public bool IsInstance { get; set; }

        [DataMember(Order = 5)]
        public string GroupName { get; set; }
    }
}
