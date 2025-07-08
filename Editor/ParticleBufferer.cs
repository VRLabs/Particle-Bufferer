using System.Linq;
using UnityEditor;
using UnityEngine;
#if VRCHAT_AVATARS
using VRC.SDK3.Avatars.Components;
#endif
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
		[MenuItem("GameObject/Effects/Buffer Particle/Particle System Renderer/Add Renderer", true, 1002)]
		public static bool AddParticleSystemRendererValidate() => SelectionHasParticleSystem();
		[MenuItem("GameObject/Effects/Buffer Particle/Particle System Renderer/Remove Renderer", true, 1003)]
		public static bool RemoveParticleSystemRendererValidate() => SelectionHasParticleSystem();

		#endregion

		[MenuItem("GameObject/Effects/Buffer Particle/From Selected", false, 1000)]
		public static void CreateBufferParticleFromSelected()
		{ 
			if (doneThisFrame) return;
			doneThisFrame = true;
			
			var particles = Selection.gameObjects.Select(go => go == null ? null : go.GetComponent<ParticleSystem>()).Where(ps => ps != null).ToList();
			if (!particles.Any()) return;
			
			ParticleSystem firstParticle = particles.Aggregate((c, d) => c.transform.GetSiblingIndex() < d.transform.GetSiblingIndex() ? c : d); // Get the lowest sibling index particle
			
			var bufferParticle = BufferParticleCreator.CreateBufferParticleFromTarget(firstParticle, withRenderer: true);
			
			var subEmitterModule = bufferParticle.subEmitters;
			for (int i = 0; i < particles.Count; i++)
			{
				if (particles[i] == firstParticle) continue;
				BufferParticleCreator.ApplySubParticleSettings(particles[i]);
				subEmitterModule.AddSubEmitter(particles[i], ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
			}
			
			var subParticles = Enumerable.Range(0, subEmitterModule.subEmittersCount).Select(x => subEmitterModule.GetSubEmitterSystem(x)).ToArray();
			UpdateAnimationsForParticles(bufferParticle, subParticles);
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
			
			foreach (var subParticle in particles)
			{
				ParticleSystem bufferParticle = BufferParticleCreator.CreateBufferParticleFromTarget(subParticle, withRenderer: true);
				UpdateAnimationsForParticles(bufferParticle, new []{subParticle});
			}

		}

		[MenuItem("GameObject/Effects/Buffer Particle/Particle System Renderer/Add Renderer", false, 1002)]
		public static void AddParticleSystemRenderer()
		{
			if (doneThisFrame) return;
			doneThisFrame = true;

			var particles = Selection.gameObjects.Select(go => go == null ? null : go.GetComponent<ParticleSystem>()).Where(ps => ps != null).ToList();
			if (!particles.Any()) return;

			foreach (var particle in particles)
            {
				if (particle.gameObject.GetComponent<ParticleSystem>() != null)
				{
					if (particle.gameObject.GetComponent<ParticleSystemRenderer>() == null)
					{
						Undo.AddComponent<ParticleSystemRenderer>(particle.gameObject);

						if (particle.GetComponent<ParticleSystemRenderer>() != null)
                        {
							particle.GetComponent<ParticleSystemRenderer>().enabled = false;
							particle.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.None;
                        }
					}
				}
			}
		}

		[MenuItem("GameObject/Effects/Buffer Particle/Particle System Renderer/Remove Renderer", false, 1003)]
		public static void RemoveParticleSystemRenderer()
		{
			if (doneThisFrame) return;
			doneThisFrame = true;

			var particles = Selection.gameObjects.Select(go => go == null ? null : go.GetComponent<ParticleSystem>()).Where(ps => ps != null).ToList();
			if (!particles.Any()) return;

			foreach (var particle in particles)
			{
				if (particle.gameObject.GetComponent<ParticleSystem>() != null)
				{
					if (particle.gameObject.GetComponent<ParticleSystemRenderer>() != null)
						Undo.DestroyObjectImmediate(particle.gameObject.GetComponent<ParticleSystemRenderer>());
				}
			}
		}

		[MenuItem("GameObject/Effects/Buffer Particle/Empty", false, 1002)]
		public static void CreateBufferParticleFromEmpty()
		{
			if (doneThisFrame) return;
			doneThisFrame = true;

			var targets = Selection.gameObjects.Any() ? Selection.gameObjects : new GameObject[] { null };
			foreach (var go in targets)
			{
				Transform parent = go == null ? null : go.transform;
				BufferParticleCreator.CreateDefaultBufferParticle(out var bufferParticle, out var subParticle, withRenderer: true);

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

		public static void UpdateAnimationsForParticles(ParticleSystem bufferParticle, ParticleSystem[] subParticles)
		{
			#if VRCHAT_AVATARS
			VRCAvatarDescriptor descriptor = bufferParticle.gameObject.GetComponentsInParent<VRCAvatarDescriptor>().FirstOrDefault();
			if (descriptor == null) return;
			
			AnimationClip[] allClips = descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers)
				.Where(x => x.animatorController != null).SelectMany(x => x.animatorController.animationClips)
				.ToArray();

			string newPath = AnimationUtility.CalculateTransformPath(bufferParticle.transform, descriptor.transform);
			string[] oldPaths = subParticles.Select(x => AnimationUtility.CalculateTransformPath(x.transform, descriptor.transform)).ToArray();
			
			try
            {
                AssetDatabase.StartAssetEditing();
                foreach (AnimationClip clip in allClips)
                {
                    EditorCurveBinding[] floatCurves = AnimationUtility.GetCurveBindings(clip);

                    foreach (EditorCurveBinding binding in floatCurves)
                    {
	                    var curveBinding = binding;
	                    AnimationCurve floatCurve = AnimationUtility.GetEditorCurve(clip, binding);
	                    if (oldPaths.Contains(curveBinding.path) && curveBinding.type == typeof(GameObject) && curveBinding.propertyName == "m_IsActive")
	                    {
		                    Undo.RecordObject(clip, "Update Animations for Particles");
		                    AnimationUtility.SetEditorCurve(clip, curveBinding, null);
		                    curveBinding.path = newPath;
		                    AnimationUtility.SetEditorCurve(clip, curveBinding, floatCurve);
		                    EditorUtility.SetDirty(clip);
	                    }
                    }
                }
            }
            finally { AssetDatabase.StopAssetEditing();  }
			#endif
		}
	}
}