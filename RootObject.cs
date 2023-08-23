using System.Xml;

namespace SimpleMultithreadedAsuncHttpServer
{
    public abstract class RootObject
    {
        public long Signature { get; set; }
        public long ClientID { get; set; }
        public long NetworkID { get; set; }
        public abstract void Init(XmlDocument xml);
        public abstract void Update();
        public abstract string ToXMLString();
    }
}