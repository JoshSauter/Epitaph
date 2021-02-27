﻿using System;
using System.Linq;
using UnityEngine;
using EpitaphUtils;

namespace Saving {
    [Serializable]
    public abstract class SerializableSaveObject {
        public string ID;
        // AssociationID is the UUID component of ID, or, if there is none, just ID
        // It is used to find all associated objects to be deregistered if a save object is Destroyed while unloaded
        public string associationID;
        public string sceneName;
    }

    [Serializable]
    public abstract class SerializableSaveObject<T> : SerializableSaveObject where T : MonoBehaviour, ISaveableObject {
        // Must be overridden by inheriting classes
        protected SerializableSaveObject(T saveableScript) {
            this.ID = saveableScript.ID;
            string lastPart = saveableScript.ID.Split('_').Last();
            this.associationID = lastPart.IsGuid() ? lastPart : this.ID;
            this.sceneName = saveableScript.gameObject.scene.name;
        }
        
        public abstract void LoadSave(T saveableScript);

        /// <summary>
        /// Unregisters this save object and any associated save object while in an unloaded scene
        /// </summary>
        public virtual void Destroy() {
            SaveManagerForScene saveForScene = SaveManager.GetOrCreateSaveManagerForScene(sceneName);
            saveForScene.UnregisterSaveableObject(ID);
            saveForScene.UnregisterAllAssociatedObjects(associationID);
        }
    }
}