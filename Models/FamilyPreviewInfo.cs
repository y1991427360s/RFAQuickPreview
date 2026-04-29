using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace RFAQuickPreview.Models
{
    [DataContract]
    public class FamilyPreviewInfo
    {
        [DataMember(Order = 1)]
        public string FileName { get; set; }

        [DataMember(Order = 2)]
        public string FullPath { get; set; }

        [DataMember(Order = 3)]
        public long FileSizeBytes { get; set; }

        [DataMember(Order = 4)]
        public DateTime ModifiedTimeUtc { get; set; }

        [DataMember(Order = 5)]
        public string FamilyName { get; set; }

        [DataMember(Order = 6)]
        public string FamilyCategory { get; set; }

        [DataMember(Order = 7)]
        public List<string> TypeNames { get; set; } = new List<string>();

        [DataMember(Order = 8)]
        public List<FamilyParameterInfo> Parameters { get; set; } = new List<FamilyParameterInfo>();

        [DataMember(Order = 9)]
        public string ThumbnailPath { get; set; }

        [DataMember(Order = 10)]
        public string ErrorMessage { get; set; }

        [DataMember(Order = 11)]
        public double FrontViewWidthFeet { get; set; }

        [DataMember(Order = 12)]
        public double FrontViewHeightFeet { get; set; }

        [DataMember(Order = 13)]
        public double LeftViewWidthFeet { get; set; }

        [IgnoreDataMember]
        public int TypeCount => TypeNames == null ? 0 : TypeNames.Count;

        [IgnoreDataMember]
        public string FileSizeText
        {
            get
            {
                if (FileSizeBytes >= 1024L * 1024L)
                {
                    return (FileSizeBytes / 1024d / 1024d).ToString("0.0") + " MB";
                }

                return (FileSizeBytes / 1024d).ToString("0.0") + " KB";
            }
        }

        public static FamilyPreviewInfo FromFile(string path)
        {
            var file = new FileInfo(path);
            return new FamilyPreviewInfo
            {
                FileName = file.Name,
                FullPath = file.FullName,
                FileSizeBytes = file.Length,
                ModifiedTimeUtc = file.LastWriteTimeUtc,
                FamilyName = Path.GetFileNameWithoutExtension(file.Name)
            };
        }
    }
}
