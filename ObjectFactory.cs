namespace SimpleMultithreadedAsuncHttpServer
{
    static class ObjectFactory
    {
        private static readonly Dictionary<long, Func<RootObject>> _map = new Dictionary<long, Func<RootObject>>();
        static ObjectFactory()
        {
            _map[0] = () => new CatServer();
            _map[1] = () => new MouseServer();
            _map[2] = () => new YarnServer();
        }
        public static RootObject Create(long Signature)
        {
            var creator = GetCreator(Signature);
            if (creator == null)
                throw new ArgumentException("Signature");
            return creator();
        }
        private static Func<RootObject>? GetCreator(long Signature)
        {
            Func<RootObject> creator;
            if (_map.TryGetValue(Signature, out creator))
                return creator;
            else
                return null;
        }
    }
}