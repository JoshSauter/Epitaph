﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using PortalMechanics;

// Player camera is already a child of the player, but we want it to act like it's lerping its position towards the player instead
public class CameraFollow : MonoBehaviour {
	bool shouldFollow = true;
	public float currentLerpSpeed = 45f;		// Can be set by external scripts to slow the camera's lerp speed for a short time
	float desiredLerpSpeed = 45f;				// currentLerpSpeed will approach this value after not being changed for a while
	public Vector3 relativeStartPosition;
	public Vector3 relativePositionLastFrame;	// Used in restoring position of camera after jump-cut movement of player
	public Vector3 worldPositionLastFrame;

	Headbob headbob;

	// DEBUG:
	//float maxFollowDistance = 0f;

	private void Start() {
		headbob = transform.parent.GetComponent<Headbob>();

		relativeStartPosition = transform.localPosition;
		worldPositionLastFrame = transform.position;
		TeleportEnter.OnAnyTeleportSimple += RecalculateWorldPositionLastFrame;
		Portal.OnAnyPortalTeleportSimple += (obj) => { if (obj.TaggedAsPlayer()) RecalculateWorldPositionLastFrame(); };
		Player.instance.look.OnViewLockBegin += HandleViewLockBegin;
		Player.instance.look.OnViewUnlockEnd += HandleViewUnlockEnd;
	}

	void LateUpdate() {
		if (!shouldFollow) return;

		Vector3 destination = transform.parent.TransformPoint(relativeStartPosition);

		//float distanceBetweenCamAndPlayerBefore = Vector3.Distance(worldPositionLastFrame, destination);
		transform.position = Vector3.Lerp(worldPositionLastFrame, destination, currentLerpSpeed * Time.deltaTime);
		//float distanceBetweenCamAndPlayer = Vector3.Distance(transform.position, destination);

		//if (distanceBetweenCamAndPlayer > maxFollowDistance) {
		//	maxFollowDistance = distanceBetweenCamAndPlayer;
		//	Debug.LogWarning("New max follow distance: " + maxFollowDistance);
		//}

		Debug.DrawRay(worldPositionLastFrame, transform.position - worldPositionLastFrame, Color.magenta);

		worldPositionLastFrame = transform.position;
		relativePositionLastFrame = transform.localPosition;

		transform.position -= headbob.curBobAmount * -Player.instance.transform.up;

		currentLerpSpeed = Mathf.Lerp(currentLerpSpeed, desiredLerpSpeed, Time.deltaTime);
    }

	// Restore the relative offset of worldPositionLastFrame after a jump-cut movement of the player
	public void RecalculateWorldPositionLastFrame() {
		worldPositionLastFrame = transform.parent.TransformPoint(relativePositionLastFrame);
	}

	void HandleViewLockBegin() {
		shouldFollow = false;
	}

	void HandleViewUnlockEnd() {
		shouldFollow = true;
		RecalculateWorldPositionLastFrame();
	}
}
