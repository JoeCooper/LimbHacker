# LimbHacker
Limb Hacker cuts skinned mesh characters in Unity3D.
## History
Limb Hacker is a fork of [Turbo Slicer](http://noblemuffins.com/?page_id=452) ([Unity Asset Store](https://www.assetstore.unity3d.com/en/#!/content/73236)).

Limb Hacker was initially released to the Unity Asset Store as a product, however, due to apparently unsolvable problems with the algorithm I have revoked it.

A client who wishes to remain anonymous funded this update which adds threading and heavy use of collection pooling.
## Acknowledgments
Tangent recalculation is based on the work of Eric Lengyel in his 2001 paper "Computing Tangent Space Basis Vectors for an Arbitrary Mesh." We've uploaded a copy of this document [here](https://drive.google.com/file/d/0B_EVqkX40JG3bkhGQnJlSVlRdU0/view).

John Ratcliff, a software engineer at NVIDIA, wrote the basic Plane-Triangle split in C++ and his code can be found [here](http://codesuppository.blogspot.com/2006/03/plane-triangle-splitting.html). This kit began as a translation of his code into C#, but has since been heavily expanded upon.

A vector-vector transformation algorithm used is pulled from a 1992 forum post by Benjamin Zhu, then an employee of Silicon Graphics Inc. The thread can be found [here](http://steve.hollasch.net/cgindex/math/rotvecs.html).

## Before You Start
You can access Limb Hacker via the static property LimbHackerAgent.instance. An instance in the scene will be created if one does not already exist.!

## Preparing an object
To slice an object, Limb Hacker needs to be able to ﬁnd a SkinnedMeshRenderer. It does support meshes with multiple materials; it does support multiple skinned mesh renderers.

To conﬁgure Limb Hacker’s behavior, you need to add the Hackable component to this object. When you provide an object to Limb Hacker, it will try to ﬁnd a single Hackable component either on it or in its children and use the conﬁguration described there.

ToRagdollOrNot is a component responsible for determining whether or not a slice results in the entity becoming a ragdoll. You can use it (it is conﬁgurable) or write your own decider by extending the abstract class AbstractSliceHandler. Place your slice handler component to the same object as the Hackable component.

### Ideal Mesh

The slicer is designed assuming it will be dealing with closed, textured meshes. Meshes which have layered, hidden geometry or triangles which pass through each other may result in inﬁll anomalies. (The “inﬁll” is the geometry made up to cover holes made by the slice.) An ideal mesh has geometry like so:

![Example](https://raw.githubusercontent.com/NobleMuffins/LimbHacker/master/Images/idealMesh.png)

The closed surface means that if you slice it pretty much anywhere, you will get a cross section with closed polygons.

### Infill
The hole made by the slice can be ﬁlled in with any texture material provided using the Hackable component’s “Inﬁll Material” property.

![Example](https://raw.githubusercontent.com/NobleMuffins/LimbHacker/master/Images/hackable.png)

The former is default; it is both faster and more reliable. You may drop in a texture and stop reading here, however I will go into detail in case you are encountering artifacts.

Limb Hacker is developed from a prior product – Turbo Slicer – which is designed to rapidly slice whole objects repeatedly. The italicized words are key and inﬂuence its design, including how it differs from Limb Hacker. When an object like a donut is sliced, the cross section of the slice will often feature multiple polygons. If a naïve or crude inﬁll is applied, there will be extraneous polygons between the slice holes. Even if there is only one polygon, it may have an irregular shape which is lends itself to artifacts. The meticulous inﬁll procedure was developed for this; it is able to distinguish multiple polygons in a cross section and ﬁll them with geometry with aligned UVs. It s the only inﬁll algorithm in Turbo Slicer.

However, it has a problem that is exacerbated in Limb Hacker; if the slices itself is imperfect, it will fail to read it and abort; no inﬁll will occur. Limb Hacker attempts to ignore irrelevant vertices, and occasionally ignored vertices overlap with the slice plane. (Slicing across the shoulders is likely to do this, depending on how a mesh is skinned.)

Limb Hacker, however, is not intended to slice whole models; it is meant to slice through character limbs. The vast majority of slices have cross sections which are single, regular polygons or at least appear as such from a distance.

Therefore, a naïve, “sloppy” inﬁller has been added to Limb Hacker. It does not attempt to distinguish polygons (a delicate process) and creates the inﬁll using a simple triangle fan.

Here we see, using a test pattern to illustrate one difference, the difference in texture mapping from the sloppy inﬁll (left) next to the meticulous inﬁll (right):

![Example](https://raw.githubusercontent.com/NobleMuffins/LimbHacker/master/Images/infillers.png)

In motion, and with organic textures, the difference is invisible. The sloppy inﬁller will also perform passable inﬁlls in situations where the meticulous inﬁll will outright refuse.

When should you use the meticulous inﬁll? There may be some edge cases – such as the forearm of a human skeleton – where a “bone” is depicted in the mesh with multiple objects and only the meticulous inﬁll can correctly ﬁll each hole independently.

If slicing a particular bone looks bad with either, you can abstain from slicing that bone by marking it unseverable. How to do this is explained later in this document.

One other difference between Limb Hacker and Turbo Slicer is the requirement in Turbo Slicer that the inﬁll texture be atlassed. This is the case there due to its goal of slicing objects to shreds from multiple angles; Unity treats each material as a “sub mesh”, which means that if what appears to be a single piece of geometry has multiple materials, it will actually be composed to separate, open sub meshes rather than one closed whole. This will break inﬁll on subsequent slices. (This also reduces draw calls and texture count; important on lower end mobiles.) Whereas Limb Hacker does not have this design requirement, and using a single material is easier for the user, we have changed Limb Hacker to use a single material.

### Alternate Prefab

This is your character’s rag-doll prefab. Limb Hacker will use this to perform a slice. Its bone hierarchy must match the original character. If you leave it blank, the character will not become a rag-doll.

### To Ragdoll or Not

When Limb Hacker performs a sever, it yields two objects, each containing part of the original’s geometry. It must decide whether new objects are based on the original object, or the ragdoll (the “alternate prefab”).

You can write the code for this yourself by extending AbstractSliceHandler (described below) and adding your component to the object, or you can use our pre-made component, ToRagdollOrNot.

![Example](https://raw.githubusercontent.com/NobleMuffins/LimbHacker/master/Images/toRagdollOrNot.png)

This component is called upon during the slice. It checks for the presence of bones in any given slice result. In the demo, we list the head, foot_L and foot_R bones by adding their transforms to the Bones list.

In the included demos, the part that remains a whole character with agency (not a ragdoll) is the one that has the head, left foot and right foot. (We could add more but that would be superﬂuous). Therefore any part that does not have the head, left foot and right foot is not a live character and needs to become a ragdoll.

If you want to copy the behavior in our demo, go ahead and copy our settings.

#### In Detail

In boolean terms, we might say:

    becomeRagdoll = NOT ( has(Head) AND has(foot_L) AND has(foot_R) )

So we set the “group rule” to “and”, and the “totality rule” to “not”. (The various items’ presence will be combined using the and operator and the whole will be inverted.)

Let’s see how this might play out in practice.

##### Example 1
Suppose we shoot off his hand. We now have two entities; one which has only the severed hand, and the other which possesses the head and both feed. For each of those entities, we evaluate the data like so:

    becomeRagdoll = NOT ( FALSE AND FALSE AND FALSE ) = TRUE
    becomeRagdoll = NOT ( TRUE AND TRUE AND TRUE ) = FALSE

So we see one of the resultant entities will become a ragdoll, and the other will not. In the demo, this means the hand will fall to the ground (it is a ragdoll, governed by physics) while the other result – the live character – will remain governed by its AI and drop its gun and run off.

##### Example 2

Suppose we sever it at the head. We now have two entities; one with the head, and the other with the rest of the body. If we drop each entities’ presence data into the function, we see these:

    becomeRagdoll = NOT ( FALSE AND TRUE AND TRUE ) = TRUE

    becomeRagdoll = NOT ( TRUE AND FALSE AND FALSE ) = TRUE 

So both pieces are ragdolliﬁed.

##### Whut?

If you want to mimic the behavior of the demo, go ahead and copy our settings.

#### Abstract Slice Handler

This is an abstract class that inherits from MonoBehavior. You may extend and implement its cloneAlternate method. (Please ignore its handleSlice method; this is used for Turbo Slicer.)

The clone alternate method permits you to decide if a given slice half will be based on the original object or the alternate prefab (usually a ragdoll).

    public virtual bool cloneAlternate ( Dictionary<string,bool> hierarchyPresence ) {
        bool useAlternatePrefab;  // ...  return useAlternatePrefab;
    }

When a slice occurs, for each half this method will be called with a dictionary describing which bones are present. You could, for example, return true if the head is not present, or return true if the head is the only item present, and this would cause the severing of a head to convert both resulting halves to a ragdoll. ToRagdollOrNot derives from this class; you may examine it as an example.

#### Slice By Point

The Limb Hacker API (described later) offers a method to slice by a given point in world space instead of specifying a joint. You might use this if – for example – you have a point in world space from a ray cast or a collision and want to slice whatever it looks like it’s supposed to slice.

However this requires conﬁguration. Not all slices look good; in testing, we found that slicing (for example) the character’s collar bone joint yielded ugly results. The Hackable component lets you decide which bones are severable.

![Example](https://raw.githubusercontent.com/NobleMuffins/LimbHacker/master/Images/severables.png)

Only bones will be selectable. Objects attached to bones in the hierarchy will follow their parent bones.

With this information, the severByPoint method can avoid artifacts.

### Limb Hacker API
#### Sever By Joint
    void SeverByJoint(GameObject subject, string jointName, float rootTipProgression, Vector3? planeNormal);

Sever by joint will hack off part of the character from any joint speciﬁed by name.

This may finish on a later frame.

##### Root Tip Progression

Root tip progression is a ﬂoat with a range [0,1) that tells where between the speciﬁed joint and its child you want the slice to occur. (If it has multiple children, it will take their mean position.) For example if we gave it the bone name of the left elbow and a roottip-progression of 0.5, it would slice halfway through the left forearm.

##### Plane Normal

Plane normal tilts the slice plane. The attachable slicer delivers angle information. However not all tilts are sensible. A ninety degree tilt, for example, can’t slice the skeleton even if the mesh could hypothetically be sliced any way we please. Limb Hacker will attempt to restrict given plane normals to sensible tilts to avoid artifacts.

#### Determine Slice
    static bool DetermineSlice(Hackable hackable, Vector3 pointInWorldSpace, ref string boneName, ref float rootTipProgression);

Given a reference to a hackable component, and a point in world space, this method will find the name of the nearest matching bone. It will also answer where along that bone the slice should occur.

### Contact

If you would like support or custom modification to this product, author Joe Cooper is available [as a freelancer](https://www.upwork.com/freelancers/~01e2723140c30fc314).

If you purchased Limb Hacker during its time on the Unity Asset Store, write us at [support@noblemuffins.com](mailto:support@noblemuffins.com).