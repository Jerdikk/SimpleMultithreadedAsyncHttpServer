using System.Xml;

namespace SimpleMultithreadedAsuncHttpServer
{
    public abstract class RootObject
    {
        public long Signature;
        public long NetworkID;
        public abstract void Init(XmlDocument xml);
        public abstract void Update();
        public abstract string ToXMLString();
        public long GetNetType() { return Signature; }
        public long GetNetworkID() { return NetworkID; }
    }
}