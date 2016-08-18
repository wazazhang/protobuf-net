using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ProtoBuf.ServiceModel
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class ProtoCommentAttribute : Attribute
    {
        public ProtoCommentAttribute(string description)
        {
            Description = description;
        }
        public string Description { get; private set; }
    }
}
