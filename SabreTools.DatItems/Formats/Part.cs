﻿using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;
using SabreTools.Core;

namespace SabreTools.DatItems.Formats
{
    /// <summary>
    /// SoftwareList part information
    /// </summary>
    /// <remarks>One Part can contain multiple PartFeature, DataArea, DiskArea, and DipSwitch items</remarks>
    [JsonObject("part"), XmlRoot("part")]
    public class Part : DatItem
    {
        #region Fields

        [JsonProperty("name"), XmlElement("name")]
        public string Name { get; set; }

        [JsonProperty("interface"), XmlElement("interface")]
        public string Interface { get; set; }
    
        [JsonProperty("features", DefaultValueHandling = DefaultValueHandling.Ignore), XmlElement("features")]
        public List<PartFeature> Features { get; set; }

        [JsonIgnore]
        public bool FeaturesSpecified { get { return Features != null && Features.Count > 0; } }

        #endregion

        #region Accessors

        /// <inheritdoc/>
        public override string GetName() => Name;

        /// <inheritdoc/>
        public override void SetName(string name) => Name = name;

        #endregion

        #region Constructors

        /// <summary>
        /// Create a default, empty Part object
        /// </summary>
        public Part()
        {
            Name = string.Empty;
            ItemType = ItemType.Part;
        }

        #endregion

        #region Cloning Methods

        /// <inheritdoc/>
        public override object Clone()
        {
            return new Part()
            {
                ItemType = this.ItemType,
                DupeType = this.DupeType,

                Machine = this.Machine.Clone() as Machine,
                Source = this.Source.Clone() as Source,
                Remove = this.Remove,

                Name = this.Name,
                Interface = this.Interface,
                Features = this.Features,
            };
        }

        #endregion

        #region Comparision Methods

        /// <inheritdoc/>
        public override bool Equals(DatItem other)
        {
            // If we don't have a Part, return false
            if (ItemType != other.ItemType)
                return false;

            // Otherwise, treat it as a Part
            Part newOther = other as Part;

            // If the Part information matches
            bool match = (Name == newOther.Name
                && Interface == newOther.Interface);
            if (!match)
                return match;

            // If the features match
            if (FeaturesSpecified)
            {
                foreach (PartFeature partFeature in Features)
                {
                    match &= newOther.Features.Contains(partFeature);
                }
            }

            return match;
        }

        #endregion
    }
}
