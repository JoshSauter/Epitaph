﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundTextureSlide : MonoBehaviour {
    Material groundMat;

    // Use this for initialization
    void Start () {
        groundMat = GetComponent<Renderer>().sharedMaterial;

    }

    private void OnEnable() {
        TeleportEnter.OnAnyTeleport += Slide;
    }

    private void OnDisable() {
        TeleportEnter.OnAnyTeleport -= Slide;
    }

    void Slide(Vector3 displacement) {
        Vector2 groundTextureDisplacement = Vector2.Scale(new Vector2(displacement.x, displacement.z), new Vector2(1f / groundMat.mainTextureScale.x, 1f / groundMat.mainTextureScale.y));
        // Scale by object's transform scale (times 10 because planes are inherently 10x larger than their scale says they are)
        groundTextureDisplacement = 10 * Vector2.Scale(groundTextureDisplacement, new Vector2(1f / transform.lossyScale.x, 1f / transform.lossyScale.z));

        groundMat.mainTextureOffset -= groundTextureDisplacement;
    }
}
