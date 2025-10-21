using System.Collections.Generic;
using System.Linq;

namespace ME.BECS {

    public readonly struct ObjectItem {

        public readonly UnityEngine.Object source;
        public readonly UnityEngine.AddressableAssets.AssetReference sourceReference;
        public readonly System.Type sourceType;
        public readonly uint sourceId;
        public readonly bool isGameObject;
        public readonly IObjectItemData data;
        
        public ObjectItem(ItemInfo data) {
            this.source = data.source;
            this.sourceReference = data.sourceReference;
            this.sourceId = data.sourceId;
            this.sourceType = data.sourceType != null ? System.Type.GetType(data.sourceType) : null;
            this.data = data.customData;
            this.isGameObject = data.isGameObject;
        }

        public bool IsValid() {
            if (this.source == null && this.sourceType == null) {
                return false;
            }
            return true;
        }

        public T Load<T>() where T : UnityEngine.Object {
            if (this.source != null) {
                if (this.source is T obj) return obj;
                return null;
            }
            if (this.sourceReference == null || string.IsNullOrEmpty(this.sourceReference.AssetGUID) == true) return null;
            #if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying == false) {
                var obj = this.sourceReference.editorAsset;
                if (this.isGameObject == true && obj is UnityEngine.GameObject goEditor) {
                    return goEditor.GetComponent<T>();
                }
                return obj as T;
            }
            #endif
            if (this.isGameObject == true) {
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.Object> op;
                if (this.sourceReference.OperationHandle.IsValid() == true) {
                    op = this.sourceReference.OperationHandle.Convert<UnityEngine.Object>();
                } else {
                    op = this.sourceReference.LoadAssetAsync<UnityEngine.Object>();
                    op.WaitForCompletion();
                }

                if (op.Result is UnityEngine.GameObject go) {
                    return go.GetComponent<T>();
                }
                return op.Result as T;
            } else {
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T> op;
                if (this.sourceReference.OperationHandle.IsValid() == true) {
                    op = this.sourceReference.OperationHandle.Convert<T>();
                } else {
                    op = this.sourceReference.LoadAssetAsync<T>();
                    op.WaitForCompletion();
                }
                return op.Result;
            }
        }

        public int GetInstanceID() {
            if (this.source != null) return this.source.GetInstanceID();
            return (int)this.sourceId;
        }

        public bool Is<T>() {
            if (this.source is T) return true;
            if (typeof(T).IsAssignableFrom(this.sourceType) == true) return true;
            return false;
        }

    }

    public interface IObjectItemData {

        bool IsValid(UnityEngine.Object obj);
        void Validate(UnityEngine.Object obj);

    }
    
    [System.Serializable]
    public struct ItemInfo : System.IEquatable<ItemInfo> {

        public UnityEngine.Object source;
        public UnityEngine.AddressableAssets.AssetReference sourceReference;
        public bool isGameObject;
        public string sourceType;
        public uint sourceId;
        
        [UnityEngine.SerializeReference]
        public IObjectItemData customData;
        
        public void CleanUpLoadedAssets() {
            if (this.sourceReference.IsValid() == true) this.sourceReference.ReleaseAsset();
        }

        public bool Equals(ItemInfo other) {
            return this.source == other.source && Equals(this.sourceReference, other.sourceReference) && this.sourceType == other.sourceType && this.sourceId == other.sourceId && Equals(this.customData, other.customData);
        }

        public override bool Equals(object obj) {
            return obj is ItemInfo other && this.Equals(other);
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(this.source, this.sourceReference, this.sourceType, this.sourceId, this.customData);
        }

        public bool Is<T>(bool ignoreErrors = false) {
            if (this.source is T) return true;
            if (string.IsNullOrEmpty(this.sourceType) == true) {
                if (ignoreErrors == false) UnityEngine.Debug.LogError($"SourceType is empty for source {this.source} ({this.sourceReference}) isGameObject: {this.isGameObject} sourceId: {this.sourceId}");
            } else {
                if (typeof(T).IsAssignableFrom(System.Type.GetType(this.sourceType)) == true) return true;
            }
            return false;
        }
        
        public bool Is(UnityEngine.Object obj) {
            if (this.source == obj) return true;
            #if UNITY_EDITOR
            if (this.sourceReference != null) {
                if (obj is UnityEngine.Component comp) {
                    if (this.sourceReference.editorAsset == comp.gameObject) return true;
                }
                if (this.sourceReference.editorAsset == obj) return true;
            }
            #endif
            return false;
        }

        public bool IsValid() {
            #if UNITY_EDITOR
            return this.source != null || this.sourceReference.editorAsset != null;
            #else
            return true;
            #endif
        }

    }

    public class ObjectReferenceRegistryData : UnityEngine.ScriptableObject {

        public ItemInfo[] items = System.Array.Empty<ItemInfo>();
        public ObjectReferenceRegistryItem[] objects = System.Array.Empty<ObjectReferenceRegistryItem>();

        internal uint sourceId;
        private readonly Dictionary<uint, ItemInfo> itemLookup = new Dictionary<uint, ItemInfo>();

        public bool ValidateRemoved() {
            var result = false;
            var removedObjects = new System.Collections.Generic.List<ObjectReferenceRegistryItem>();
            foreach (var obj in this.objects) {
                if (obj.IsValid() == false) {
                    removedObjects.Add(obj);
                    result = true;
                }
            }
            foreach (var obj in removedObjects) {
                UnityEngine.Debug.Log("Removed: " + obj, obj);
                UnityEditor.AssetDatabase.DeleteAsset(UnityEditor.AssetDatabase.GetAssetPath(obj));
            }

            if (result == true) {
                this.Validate();
            }
            return result;
        }
        
        [UnityEngine.ContextMenu("Call OnValidate")]
        public void Validate() {

            var newObjects = new System.Collections.Generic.List<ItemInfo>();
            {
                var list = this.objects.ToList();
                list.RemoveAll(x => x == null);
                this.objects = list.ToArray();
            }
            foreach (var item in this.items) {
                var found = false;
                foreach (var obj in this.objects) {
                    if (obj.data.Equals(item) == true) {
                        found = true;
                        break;
                    }
                }
                if (found == false) {
                    newObjects.Add(item);
                }
            }
            this.items = System.Array.Empty<ItemInfo>();
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                var curDir = System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(this));
                var dir = $"{curDir}/ObjectReferenceRegistry";
                if (System.IO.Directory.Exists(dir) == false) {
                    System.IO.Directory.CreateDirectory(dir);
                }

                foreach (var obj in newObjects) {
                    var instance = UnityEngine.ScriptableObject.CreateInstance<ObjectReferenceRegistryItem>();
                    instance.name = obj.sourceId.ToString();
                    instance.data = obj;
                    var path = $"{dir}/{instance.name}.asset";
                    UnityEditor.AssetDatabase.CreateAsset(instance, path);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }

                var items = UnityEditor.AssetDatabase.FindAssets("t:ObjectReferenceRegistryItem", new string[] { dir });
                this.objects = new ObjectReferenceRegistryItem[items.Length];
                var index = 0;
                foreach (var guid in items) {
                    var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ObjectReferenceRegistryItem>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    this.objects[index++] = item;
                }
            };
            #endif

        }
        
        public void Initialize() {
            this.itemLookup.Clear();
            this.sourceId = 0u;
            foreach (var item in this.objects) {
                if (this.itemLookup.TryAdd(item.data.sourceId, item.data) == false) {
                    UnityEngine.Debug.LogError($"[ObjectReference] Data contains duplicate sourceId {item.data.sourceId} {item.data.source}");
                }
                if (item.data.sourceId > this.sourceId) this.sourceId = item.data.sourceId;
            }
        }

        public void CleanUpLoadedAssets() {
            foreach (var item in this.objects) {
                item.data.CleanUpLoadedAssets();
            }
        }
        
        public ObjectItem GetObjectBySourceId(uint sourceId) {
            if (this.itemLookup.Count == 0) {
                this.Initialize();
            }
            if (this.itemLookup.TryGetValue(sourceId, out var item) == true && item.sourceType != null) {
                return new ObjectItem(item);
            }
            for (int i = 0; i < this.objects.Length; ++i) {
                if (this.objects[i]?.data.sourceId == sourceId) {
                    return new ObjectItem(this.objects[i].data);
                }
            }
            return default;
        }

        public uint Add(UnityEngine.Object source, out bool isNew) {

            isNew = false;
            if (source == null) return 0u;

            for (int i = 0; i < this.objects.Length; ++i) {
                if (this.objects[i].data.Is(source) == true) {
                    ref var item = ref this.objects[i].data;
                    return item.sourceId;
                }
            }

            isNew = true;
            var nextId = this.GetNextId(source);
            {
                // Add new item
                var item = new ItemInfo() {
                    sourceId = nextId,
                    source = source,
                };
                System.Array.Resize(ref this.items, this.items.Length + 1);
                this.items[this.items.Length - 1] = item;
                if (this.itemLookup.Count == 0) {
                    this.Initialize();
                }

                if (this.itemLookup.TryGetValue(nextId, out _) == true) {
                    this.itemLookup[nextId] = item;
                } else {
                    this.itemLookup.Add(nextId, item);
                }
                #if UNITY_EDITOR
                this.Validate();
                #endif
                return nextId;
            }

        }

        private uint GetNextId(UnityEngine.Object source) {
            #if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(source);
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            guid = $"{guid}P{path}";
            var md5Hasher = System.Security.Cryptography.MD5.Create();
            var hashed = md5Hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(guid));
            var hashId = (uint)System.BitConverter.ToInt32(hashed, 0);
            if (hashId == 0u) hashId = 1u;

            while (true) {
                // Set unique next id
                var has = false;
                for (int i = 0; i < this.objects.Length; ++i) {
                    ref var item = ref this.objects[i].data;
                    if (item.sourceId == hashId) {
                        ++hashId;
                        has = true;
                        break;
                    }
                }
                if (has == false) break;
            }

            return hashId;
            #else
            var nextId = ++this.sourceId;
            return nextId;
            #endif
        }

        public bool Remove(UnityEngine.Object source) {
            
            /*for (int i = 0; i < this.objects.Length; ++i) {
                if (this.objects[i].data.source == source) {
                    //ref var item = ref this.objects[i];
                    //if (item.referencesCount == 0u) return false;
                    //--item.referencesCount;
                    // if (item.references == 0u) {
                    //     if (this.items.Length == 1) {
                    //         this.items = System.Array.Empty<Item>();
                    //     } else {
                    //         this.items[i] = this.items[this.items.Length - 1];
                    //         System.Array.Resize(ref this.items, this.items.Length - 1);
                    //     }
                    //
                    //     return true;
                    // }
                }
            }*/

            return false;

        }

        public uint GetSourceId() {
            return this.sourceId;
        }

    }

}