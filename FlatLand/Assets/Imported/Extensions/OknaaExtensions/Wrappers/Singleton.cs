using UnityEngine;


namespace OknaaEXTENSIONS.CustomWrappers {
    public abstract class Singleton<T> : MonoBehaviour where T : Component {
        public static T Instance => _instance ??= FindObjectOfType<T>();
        private static T _instance;


        protected virtual void Dispose() { }
        
        private void OnDestroy() {
            Dispose();
            _instance = null;
        }
    }
}