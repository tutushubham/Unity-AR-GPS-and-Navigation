using UnityEngine;
using System.Collections.Generic;
using Wikitude;

public class MultipleTargetsController : SampleController
{
    private HashSet<Dinosaur> _visibleDinosaurs = new HashSet<Dinosaur>();

    public void OnImageRecognized(ImageTarget target) {
        /* Whenever a new dinosaur is recognized, keep track of it in the _visibleDinosaurs variable.
         * Because the ImageTrackable has a prefab assigned to the Drawable property, we don't need to take
         * care of instantiating the dinosaurs manually.
         */
        _visibleDinosaurs.Add(target.Drawable.transform.GetChild(0).GetComponent<Dinosaur>());
    }

    public void OnImageLost(ImageTarget target) {
        var lostDinosaur = target.Drawable.transform.GetChild(0).GetComponent<Dinosaur>();
        _visibleDinosaurs.Remove(lostDinosaur);

        /* If the lost dinosaur was engaged in battle with another dinosaur,
         * notify the other dinosaur so that it can disengage and return to its idle position.
         */
        foreach (var dinosaur in _visibleDinosaurs) {
            if (dinosaur.AttackingDinosaur == lostDinosaur) {
                dinosaur.OnAttackerDisappeared();
            } else if (dinosaur.TargetDinosaur == lostDinosaur) {
                dinosaur.OnTargetDisappeared();
            }
        }
    }

    protected override void Update() {
        base.Update();

        if (_visibleDinosaurs.Count > 1) {
            /* If we have more than two dinosaurs, try to pair them in battles. */
            Dinosaur availableDinosaur = null;
            foreach (var dinosaur in _visibleDinosaurs) {
                if (!dinosaur.InBattle) {
                    if (availableDinosaur == null) {
                        availableDinosaur = dinosaur;
                    } else {
                        availableDinosaur.Attack(dinosaur);
                        availableDinosaur = null;
                    }
                }
            }
        }
    }
}
