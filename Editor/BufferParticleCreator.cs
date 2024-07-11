using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRLabs.ParticleBufferer
{
	public class BufferParticleCreator
	{
		public static ParticleSystem CreateBufferParticleFromTarget(GameObject go)
		{
			var particle = go.GetComponent<ParticleSystem>();
			if (particle == null) return null;
			
			Undo.RecordObject(go, "Create Buffer Particle");
			go.SetActive(true);
			var bps = CreateBufferParticle(particle);
			bps.gameObject.name = go.name;
			go.name += " (Sub)";
			GameObjectUtility.EnsureUniqueNameForSibling(go);
			EditorUtility.SetDirty(go);
			return bps;
		}
		
		public static void CreateBufferParticle(out ParticleSystem bufferParticle, out ParticleSystem subParticle)
		{
			var subGameObject = new GameObject("Sub Particle");
			subParticle = subGameObject.AddComponent<ParticleSystem>();
			
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
			
			Undo.RegisterCreatedObjectUndo(subGameObject, "Create Buffer Particle");
			bufferParticle = CreateBufferParticle(subParticle);
		}
		
		public static ParticleSystem CreateBufferParticle(ParticleSystem subParticle)
		{
			ApplySubParticleSettings(subParticle);
			var bps = CreateBufferParticleBase();
			CopyTransformSettings(subParticle.transform, bps.transform);
			bps.transform.rotation = Quaternion.identity;
			GameObjectUtility.EnsureUniqueNameForSibling(bps.gameObject);
			
			bps.transform.SetSiblingIndex(subParticle.transform.GetSiblingIndex());
			
			var sem = bps.subEmitters;
			sem.enabled = true;
			sem.AddSubEmitter(subParticle, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
			return bps;
		}

		public static ParticleSystem CreateBufferParticleBase()
		{
			GameObject bufferParticleObject = new GameObject("Buffer Particle");
			bufferParticleObject.SetActive(false);
			ParticleSystem bufferParticle = bufferParticleObject.AddComponent<ParticleSystem>();
			Object.DestroyImmediate(bufferParticleObject.GetComponent<ParticleSystemRenderer>());
			Undo.RegisterCreatedObjectUndo(bufferParticleObject, "Create Buffer Particle");
			
			#region Buffer Particle Settings
			var main = bufferParticle.main;
			main.duration = 1;
			main.playOnAwake = true;
			main.loop = false;
			main.startLifetime = 1000;
			main.startSpeed = 0.0001f;
			main.maxParticles = 1;

			var em = bufferParticle.emission;
			em.rateOverTime = 0;
			em.SetBursts(new []{new ParticleSystem.Burst(0,1)});
			
			var shape = bufferParticle.shape;
			shape.enabled = false;
			#endregion

			return bufferParticle;
		}

		public static void ApplySubParticleSettings(ParticleSystem subParticle)
		{
			Undo.RecordObject(subParticle, "Apply Sub Particle Settings");
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