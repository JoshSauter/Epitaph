﻿using System;
using System.Collections;
using EpitaphUtils;
using PictureTeleportMechanics;
using PortalMechanics;
using SerializableClasses;
using UnityEngine;

namespace LevelSpecific.BehindForkTransition {
    public class TurnPortalBackOnTeleport : MonoBehaviour {
        public SerializableReference<Portal, Portal.PortalSave> portalRef;
        // GetOrNull since we only refer to the value when we know the reference is valid (when this scene is active)
        Portal portal => portalRef.GetOrNull();
        PictureTeleport pictureTeleport;

        IEnumerator Start() {
            yield return new WaitUntil(() => gameObject.IsInActiveScene());
            
            pictureTeleport = GetComponent<PictureTeleport>();
            pictureTeleport.OnPictureTeleport += () => portal.gameObject.SetActive(true);
        }
    }
}