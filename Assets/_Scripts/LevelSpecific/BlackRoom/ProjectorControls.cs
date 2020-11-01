﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;

namespace LevelSpecific.BlackRoom {
	public class ProjectorControls : MonoBehaviour {
		public LightProjector projector;

		public ButtonHold projectorSizeIncreaseButton;
		public ButtonHold projectorSizeDecreaseButton;

		public ValveControl projectorRotateValve;

		public ButtonHold projectorRotateAxisLeftButton;
		public ButtonHold projectorRotateAxisRightButton;

		public ButtonHold projectorRotateAxisDownButton;
		public ButtonHold projectorRotateAxisUpButton;

		// How many rotations of the valve does it take to rotate the light around the circumference once?
		const float valveToLightRotationRatio = 8;

		// Use this for initialization
		void Start() {
			projectorSizeIncreaseButton.OnButtonHeld += ctx => IncreaseFrustumSize();
			projectorSizeDecreaseButton.OnButtonHeld += ctx => DecreaseFrustumSize();

			projectorRotateValve.OnValveRotate += RotateProjector;

			projectorRotateAxisLeftButton.OnButtonHeld += ctx => RotateProjectorLeftOnAxis();
			projectorRotateAxisRightButton.OnButtonHeld += ctx => RotateProjectorRightOnAxis();

			projectorRotateAxisDownButton.OnButtonHeld += ctx => RotateProjectorDownOnAxis();
			projectorRotateAxisUpButton.OnButtonHeld += ctx => RotateProjectorUpOnAxis();
		}

		void IncreaseFrustumSize() {
			projector.IncreaseFrustumSize();
		}

		void DecreaseFrustumSize() {
			projector.DecreaseFrustumSize();
		}

		void RotateProjector(Angle diff) {
			projector.RotateAroundCircumference(-diff.degrees / valveToLightRotationRatio);
		}

		void RotateProjectorLeftOnAxis() {
			projector.RotateAngleLeft();
		}

		void RotateProjectorRightOnAxis() {
			projector.RotateAngleRight();
		}

		void RotateProjectorDownOnAxis() {
			projector.RotateAngleDown();
		}

		void RotateProjectorUpOnAxis() {
			projector.RotateAngleUp();
		}
	}
}