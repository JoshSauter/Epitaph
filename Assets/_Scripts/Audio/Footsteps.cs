﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Footsteps : MonoBehaviour {
	public SoundSettings sound;
	Headbob bob;
	PlayerMovement playerMovement;
	float defaultVolume;
	float curBobAmountUnamplified = 0;
	bool playerWasHeadingDownLastFrame = false;
	float timeSinceLastHit = 0f;

	// Alternates between true and false so we only play a sound every other step
	bool shouldForceStepSound = true;

    void Start() {
		playerMovement = GetComponent<PlayerMovement>();
		playerMovement.OnStaircaseStepUp += () => { PlayFootstepAtVolume(shouldForceStepSound, 0.125f); shouldForceStepSound = !shouldForceStepSound; };

		bob = GetComponent<Headbob>();
		defaultVolume = sound.volume;

		if (bob == null) {
			Debug.LogWarning("Footsteps requires headbob info");
			this.enabled = false;
		}
	}

    void LateUpdate() {
		timeSinceLastHit += Time.deltaTime;
		bool shouldForcePlay = timeSinceLastHit > 0.25f;

		Vector2 playerVelocity = playerMovement.HorizontalVelocity();
		float playerSpeed = playerVelocity.magnitude;
		sound.volume = Mathf.Lerp(defaultVolume - defaultVolume/2f, defaultVolume + defaultVolume / 2f, Mathf.InverseLerp(0f, 20f, playerSpeed));
		if (playerMovement.grounded && playerSpeed > 0.2f) {
			float thisFrameBobAmount = bob.viewBobCurve.Evaluate(bob.t);
			float thisFrameOffset = thisFrameBobAmount - curBobAmountUnamplified;
			curBobAmountUnamplified = thisFrameBobAmount;
			bool playerIsHeadingUpThisFrame = thisFrameOffset > 0;
			if (playerWasHeadingDownLastFrame && playerIsHeadingUpThisFrame) {
				PlayFootstep(shouldForcePlay);
				timeSinceLastHit = 0f;
			}
			playerWasHeadingDownLastFrame = !playerIsHeadingUpThisFrame;
		}
		else {
			if (playerWasHeadingDownLastFrame) {
				PlayFootstep(shouldForcePlay);
				timeSinceLastHit = 0f;
			}
			playerWasHeadingDownLastFrame = false;
		}
	}

	void PlayFootstepAtVolume(bool shouldForcePlay, float tempVolume) {
		float tmp = sound.volume;
		sound.volume = tempVolume;
		PlayFootstep(shouldForcePlay);
		sound.volume = tmp;
	}

	void PlayFootstep(bool shouldForcePlay) {
		SoundManager.instance.Play("footstep", sound, shouldForcePlay);
	}
}