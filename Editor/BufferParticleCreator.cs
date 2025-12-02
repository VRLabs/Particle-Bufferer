using UnityEditor;
using UnityEngine;

namespace VRLabs.ParticleBufferer
{
	public class BufferParticleCreator
	{
		public static ParticleSystem CreateBufferParticleFromTarget(ParticleSystem particle, bool withRenderer)
		{
			Undo.RecordObject(particle, "Create Particle From Target");

			ApplySubParticleSettings(particle);
			particle.gameObject.SetActive(true);

			var bufferParticle = CreateBufferParticleBase(withRenderer);
			CopyTransformSettings(particle.transform, bufferParticle.transform);
			bufferParticle.transform.rotation = Quaternion.identity;
			GameObjectUtility.EnsureUniqueNameForSibling(bufferParticle.gameObject);
			
			var subEmitterModule = bufferParticle.subEmitters;
			subEmitterModule.enabled = true;
			subEmitterModule.AddSubEmitter(particle, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
			return bufferParticle;
		}
		
		public static void CreateDefaultBufferParticle(out ParticleSystem bufferParticle, out ParticleSystem subParticle, bool withRenderer)
		{
			var subGameObject = new GameObject("Sub Particle");
			subParticle = subGameObject.AddComponent<ParticleSystem>();
			Undo.RegisterCreatedObjectUndo(subParticle.gameObject, "Create Buffer Particle");
			
			#region Default Sub Particle Settings
			var main = subParticle.main;
			main.loop = false;
			main.playOnAwake = false;
			
			var shape = subParticle.shape;
			shape.enabled = false;
			
			var emission = subParticle.emission;
			emission.rateOverTime = 0;
			emission.SetBursts(new []{new ParticleSystem.Burst(0,1)});
			#endregion
			
			bufferParticle = CreateBufferParticleFromTarget(subParticle, withRenderer);
		}

		public static ParticleSystem CreateBufferParticleBase(bool withRenderer)
		{
			GameObject bufferParticleObject = new GameObject("Buffer Particle");
			Undo.RegisterCreatedObjectUndo(bufferParticleObject, "Create Buffer Particle");
			bufferParticleObject.SetActive(false);
			ParticleSystem bufferParticle = bufferParticleObject.AddComponent<ParticleSystem>();

			if (withRenderer)
				bufferParticle.GetComponent<ParticleSystemRenderer>().enabled = false;
			else
				Object.DestroyImmediate(bufferParticleObject.GetComponent<ParticleSystemRenderer>());

			#region Buffer Particle Settings
			var main = bufferParticle.main;
			main.duration = 1;
			main.playOnAwake = true;
			main.loop = false;
			main.startLifetime = 1000;
			main.startSpeed = 0.001f;
			main.maxParticles = 1;

			var em = bufferParticle.emission;
			em.rateOverTime = 0;
			em.SetBursts(new []{new ParticleSystem.Burst(0,1)});
			
			var shape = bufferParticle.shape;
			shape.enabled = false;

			if (withRenderer)
				bufferParticle.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.None;

			#endregion

			return bufferParticle;
		}

		public static void ApplySubParticleSettings(ParticleSystem subParticle)
		{
			Undo.RecordObject(subParticle.gameObject, "Adjust Sub Particle");
			var main = subParticle.main;
			main.loop = false;
			main.playOnAwake = false;

			var em = subParticle.emission;
			var rot = em.rateOverTime;
			var rod = em.rateOverDistance;
			if (rot.mode != ParticleSystemCurveMode.Constant || rot.constantMax > 100 || rod.Evaluate(0) > 0)
				Debug.LogWarning("Target particle system has emission values that can't be converted cleanly to burst. Please adjust manually.");
			else if (rot.constantMax > 0)
			{
				var burstInterval = 1 / rot.constantMax;
				em.rateOverTime = 0;

				var bursts = new ParticleSystem.Burst[em.burstCount];
				em.GetBursts(bursts);
				ArrayUtility.Add(ref bursts, new ParticleSystem.Burst(0, 1, 1, 0, burstInterval));
				em.SetBursts(bursts);
			}
			EditorUtility.SetDirty(subParticle);
		}

		public static void CopyTransformSettings(Transform source, Transform destination)
		{
			destination.parent = source.parent;
			destination.localPosition = source.localPosition;
			destination.localRotation = source.localRotation;
			destination.localScale = source.localScale;
		}
	}
}