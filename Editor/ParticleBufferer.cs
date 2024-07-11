using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRLabs.ParticleBufferer
{
	public static class ParticleBufferer
	{
		#region HashSet
		// Ok so here's why this int exists. Prepare yourself.
		// Typically with MenuItem methods, you can pass 'MenuCommand' argument to get the Object being processed currently.
		// That's because Unity calls this method for each target of the context menu, and is SUPPOSED to pass the GameObject as the context object.
		// BUT THAT DOESN'T WORK. IT'S ALWAYS NULL.
		// So the only way to know what we right clicked on is to get the Selection.
		// But because it calls it for each selected item, IT WOULD LOOP THROUGH THE SELECTION FOR THE SAME NUMBER OF SELECTED ITEMS.
		// So we need to keep track of what we've already processed so we don't do it again.
		// And one more thing! It calls it only once if you use the GameObject toolbar menu :) I'm not accounting for that bs.
		// We love Unity!
		private static int processedCount = -1;
		#endregion
		
		public static bool SelectionHasParticleSystem() => Selection.gameObjects.Any(go => go != null && go.GetComponent<ParticleSystem>() != null);
		
		[MenuItem("GameObject/Effects/Buffer Particle/From Selected", true, 1000)]
		public static bool CreateBufferParticleFromSelectedValidate() => SelectionHasParticleSystem();

		[MenuItem("GameObject/Effects/Buffer Particle/For Each Selected", true, 1001)]
		public static bool CreateBufferParticleForEachSelectedValidate() => SelectionHasParticleSystem();

		[MenuItem("GameObject/Effects/Buffer Particle/From Selected", false, 1000)]
		public static void CreateBufferParticleFromSelected()
		{
			if (processedCount < 0) processedCount = Selection.gameObjects.Length - 1;
			if (processedCount-- != 0) return;
			var particles = Selection.gameObjects.Select(go => go == null ? null : go.GetComponent<ParticleSystem>()).Where(ps => ps != null).ToList();
			if (!particles.Any()) return;
			
			
			ParticleSystem firstParticle = particles.Aggregate((c, d) => c.transform.GetSiblingIndex() < d.transform.GetSiblingIndex() ? c : d); // Get the lowest sibling index particle
			
			var bps = BufferParticleCreator.CreateBufferParticleFromTarget(firstParticle.gameObject);
			var sem = bps.subEmitters;
			particles.Remove(firstParticle);
			for (int i = 0; i < particles.Count; i++)
			{
				BufferParticleCreator.ApplySubParticleSettings(particles[i]);
				sem.AddSubEmitter(particles[i], ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
				Undo.RecordObject(particles[i].gameObject, "Create Buffer Particle");
				particles[i].gameObject.name += $" (Sub {i + 2})";
			}
		}
		
		// Creates a buffer particle per GO with a ParticleSystem component
		[MenuItem("GameObject/Effects/Buffer Particle/For Each Selected", false, 1001)]
		public static void CreateBufferParticleForEachSelected()
		{
			if (processedCount < 0) processedCount = Selection.gameObjects.Length - 1;
			if (processedCount-- != 0) return;
			foreach (var go in Selection.gameObjects)
				BufferParticleCreator.CreateBufferParticleFromTarget(go);
		}

		[MenuItem("GameObject/Effects/Buffer Particle/Empty", false, 1002)]
		public static void CreateBufferParticleFromEmpty()
		{
			var targets = Selection.gameObjects.Any() ? Selection.gameObjects : new GameObject[] {null};
			foreach (var go in targets)
			{
				Transform parent = go == null ? null : go.transform;
				BufferParticleCreator.CreateBufferParticle(out var bp, out var sp);

				if (parent == null) continue;
				var t = bp.transform;
				BufferParticleCreator.CopyTransformSettings(parent, t);
				
				t.parent = parent;

				BufferParticleCreator.CopyTransformSettings(t, sp.transform);
				t.transform.rotation = Quaternion.identity;

				GameObjectUtility.EnsureUniqueNameForSibling(t.gameObject);
				GameObjectUtility.EnsureUniqueNameForSibling(sp.gameObject);
			}
		}
	}
}