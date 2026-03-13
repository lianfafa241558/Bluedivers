using UnityEngine;
using System.Collections;

namespace RootMotion.FinalIK {

	public class LookAtController : MonoBehaviour {

		public LookAtIK ik;

		[Header("目标平滑处理")]

		[Tooltip("要观察的目标。请勿使用分配给LookAtIK的目标变换。如果希望停止观察，请设置为null。")]
		public Transform target;

		[Range(0f, 1f)] public float weight = 0.8f;

		public Vector3 offset;

		[Tooltip("切换目标所需的时间。")]
		public float targetSwitchSmoothTime = 0.3f;

		[Tooltip("在LookAtIK权重中渐变所需的时间。")]
		public float weightSmoothTime = 0.3f;

		[Header("朝向目标转动")]

		[Tooltip("根据此标题下的参数启用平滑转向目标。")]
		public bool smoothTurnTowardsTarget = true;

		[Tooltip("使用Vector3.RotateTowards朝向目标的转动速度。")]
		public float maxRadiansDelta = 3f;

		[Tooltip("使用Vector3.RotateTowards朝向目标的移动速度。")]
		public float maxMagnitudeDelta = 3f;

		[Tooltip("朝向目标的插值速度。")]
		public float slerpSpeed = 3f;

		[Tooltip("观察目标旋转的支点位置，相对于角色的根部。")]
		public Vector3 pivotOffsetFromRoot = Vector3.up;

		[Tooltip("从第一个骨骼观察的最小距离。如果目标太近，将防止求解器失败。")]
		public float minDistance = 1f;

		[Header("根旋转")]
		[Tooltip("角色根部将在Y轴上旋转，以保持根部在观察方向的此角度内向前。")]
		[Range(0f, 180f)]
		public float maxRootAngle = 45f;

		private Transform lastTarget;
		private float switchWeight, switchWeightV;
		private float weightV;
		private Vector3 lastPosition;
		private Vector3 dir;
		private bool lastSmoothTowardsTarget;

		void Start() {
			lastPosition = ik.solver.IKPosition;
			dir = lastPosition - pivot;
		}

		void LateUpdate () {
			// If target has changed...
			if (target != lastTarget) {
                if (lastTarget == null && target != null && ik.solver.IKPositionWeight <= 0f) { 
                    lastPosition = target.position;
					dir = target.position - pivot;
					ik.solver.IKPosition = target.position + offset;
				} else {
					lastPosition = ik.solver.IKPosition;
					dir = ik.solver.IKPosition - pivot;
				}

				switchWeight = 0f;
				lastTarget = target;
			}

            // Smooth weight
            float targetWeight = target != null ? weight : 0f;
			ik.solver.IKPositionWeight = Mathf.SmoothDamp(ik.solver.IKPositionWeight, targetWeight, ref weightV, weightSmoothTime, Mathf.Infinity, deltaTime:Time.unscaledDeltaTime);
			if (ik.solver.IKPositionWeight >= 0.999f && targetWeight > ik.solver.IKPositionWeight) ik.solver.IKPositionWeight = 1f;
			if (ik.solver.IKPositionWeight <= 0.001f && targetWeight < ik.solver.IKPositionWeight) ik.solver.IKPositionWeight = 0f;

			if (ik.solver.IKPositionWeight <= 0f) return;

			// Smooth target switching
			switchWeight = Mathf.SmoothDamp(switchWeight, 1f, ref switchWeightV, targetSwitchSmoothTime);
			if (switchWeight >= 0.999f) switchWeight = 1f;

			if (target != null) {
				ik.solver.IKPosition = Vector3.Lerp(lastPosition, target.position + offset, switchWeight);
			}

			// Smooth turn towards target
			if (smoothTurnTowardsTarget != lastSmoothTowardsTarget) {
				dir = ik.solver.IKPosition - pivot;
				lastSmoothTowardsTarget = smoothTurnTowardsTarget;
			}

			if (smoothTurnTowardsTarget) {
				Vector3 targetDir = ik.solver.IKPosition - pivot;
				dir = Vector3.Slerp(dir, targetDir, Time.unscaledDeltaTime * slerpSpeed);
				dir = Vector3.RotateTowards(dir, targetDir, Time.unscaledDeltaTime * maxRadiansDelta, maxMagnitudeDelta);
				ik.solver.IKPosition = pivot + dir;
			}

			// Min distance from the pivot
			ApplyMinDistance();

			// Root rotation
			RootRotation();
		}

		//旋转瞄准方向的枢轴。
		private Vector3 pivot {
			get {
				return ik.transform.position + ik.transform.rotation * pivotOffsetFromRoot;
			}
		}

		// Make sure aiming target is not too close (might make the solver instable when the target is closer to the first bone than the last bone is).
		void ApplyMinDistance() {
			Vector3 aimFrom = pivot;
			Vector3 direction = (ik.solver.IKPosition - aimFrom);
			direction = direction.normalized * Mathf.Max(direction.magnitude, minDistance);
				
			ik.solver.IKPosition = aimFrom + direction;
		}

		// Character root will be rotate around the Y axis to keep root forward within this angle from the looking direction.
		private void RootRotation() {
			float max = Mathf.Lerp(180f, maxRootAngle, ik.solver.IKPositionWeight);

			if (max < 180f) {
				Vector3 faceDirLocal = Quaternion.Inverse(ik.transform.rotation) * (ik.solver.IKPosition - pivot);
				float angle = Mathf.Atan2(faceDirLocal.x, faceDirLocal.z) * Mathf.Rad2Deg;

				float rotation = 0f;

				if (angle > max) {
					rotation = angle - max;
				}
				if (angle < -max) {
					rotation = angle + max;
				}

				ik.transform.rotation = Quaternion.AngleAxis(rotation, ik.transform.up) * ik.transform.rotation;		
			}
		}
	}
}
