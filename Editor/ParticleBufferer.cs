using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRLabs.ParticleBufferer
{
	public static class ParticleBufferer
	{
		static ParticleBufferer ()
		{
			EditorApplication.update += () => doneThisFrame = false;
		}
		
		private static bool doneThisFrame = false;
		
		#region Validation
		
		public static bool SelectionHasParticleSystem() => Selection.gameObjects.Any(go => go != null && go.GetComponent<ParticleSystem>() != null);
		
		[MenuItem("GameObject/Effects/Buffer Particle/From Selected", true, 1000)]
		public static bool CreateBufferParticleFromSelectedValidate() => SelectionHasParticleSystem();

		[MenuItem("GameObject/Effects/Buffer Particle/For Each Selected", true, 1001)]
		public static bool CreateBufferParticleForEachSelectedValidate() => SelectionHasParticleSystem();
		
		#endregion

		[MenuItem("GameObject/Effects/Buffer Particle/From Selected", false, 1000)]
		public static void CreateBufferParticleFromSelected()
		{ 
			if (doneThisFrame) return;
			doneThisFrame = true;
			
			var particles = Selection.gameObjects.Select(go => go == null ? null : go.GetComponent<ParticleSystem>()).Where(ps => ps != null).ToList();
			if (!particles.Any()) return;
			
			ParticleSystem firstParticle = particles.Aggregate((c, d) => c.transform.GetSiblingIndex() < d.transform.GetSiblingIndex() ? c : d); // Get the lowest sibling index particle
			
			var bufferParticle = BufferParticleCreator.CreateBufferParticleFromTarget(firstParticle);
			
			var subEmitterModule = bufferParticle.subEmitters;
			for (int i = 0; i < particles.Count; i++)
			{
				if (particles[i] == firstParticle) continue;
				BufferParticleCreator.ApplySubParticleSettings(particles[i]);
				subEmitterModule.AddSubEmitter(particles[i], ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
			}
		}
		
		[MenuItem("GameObject/Effects/Buffer Particle/For Each Selected", false, 1001)]
		public static void CreateBufferParticleForEachSelected()
		{
			if (doneThisFrame) return;
			doneThisFrame = true;

			var particles = Selection.gameObjects
				.Select(go => go == null ? null : go.GetComponent<ParticleSystem>())
				.Where(ps => ps != null)
				.ToList();
			foreach (var particle in particles)
			{
				BufferParticleCreator.CreateBufferParticleFromTarget(particle);
			}
		}

		[MenuItem("GameObject/Effects/Buffer Particle/Empty", false, 1002)]
		public static void CreateBufferParticleFromEmpty()
		{
			if (doneThisFrame) return;
			doneThisFrame = true;
			
			var targets = Selection.gameObjects.Any() ? Selection.gameObjects : new GameObject[] {null};
			foreach (var go in targets)
			{
				Transform parent = go == null ? null : go.transform;
				BufferParticleCreator.CreateDefaultBufferParticle(out var bufferParticle, out var subParticle);

				if (parent == null)
				{
					GameObjectUtility.EnsureUniqueNameForSibling(bufferParticle.gameObject);
					GameObjectUtility.EnsureUniqueNameForSibling(subParticle.gameObject);
					continue;
				}
				
				BufferParticleCreator.CopyTransformSettings(parent, bufferParticle.transform);
				BufferParticleCreator.CopyTransformSettings(bufferParticle.transform, subParticle.transform);
				
				bufferParticle.transform.parent = parent;
				subParticle.transform.parent = parent;
				bufferParticle.transform.rotation = Quaternion.identity;

				GameObjectUtility.EnsureUniqueNameForSibling(bufferParticle.gameObject);
				GameObjectUtility.EnsureUniqueNameForSibling(subParticle.gameObject);
			}
		}
	}
}