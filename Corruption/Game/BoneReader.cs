using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Corruption.Game;

/// Read-only world-space bone positions for any drawn player character, adapted from PoseKit (AGPL-3.0). Framework
/// thread.
public static unsafe class BoneReader
{
    /// Every named bone's world position, and the model's yaw. Null when the character isn't drawn as a human.
    public static (Dictionary<string, Vector3> Bones, float Yaw)? Snapshot(IPlayerCharacter character)
    {
        var native = (Character*)character.Address;
        if (native == null || native->DrawObject == null) return null;
        if (native->DrawObject->GetObjectType() != ObjectType.CharacterBase) return null;
        var characterBase = (CharacterBase*)native->DrawObject;
        if (characterBase->GetModelType() != CharacterBase.ModelType.Human) return null;
        var skeleton = characterBase->Skeleton;
        if (skeleton == null) return null;

        // Copied to System.Numerics types; mixing vector types makes operators ambiguous.
        var transform = skeleton->Transform;
        var position = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
        var rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);
        var scale = new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
        var forward = Vector3.Transform(Vector3.UnitZ, rotation);

        var bones = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        for (var p = 0; p < skeleton->PartialSkeletonCount; p++)
        {
            var pose = skeleton->PartialSkeletons[p].GetHavokPose(0);
            if (pose == null || pose->Skeleton == null) continue;
            var list = pose->Skeleton->Bones;
            for (var i = 0; i < list.Length; i++)
            {
                var name = list[i].Name.String;
                if (string.IsNullOrEmpty(name) || bones.ContainsKey(name)) continue;
                var model = pose->AccessBoneModelSpace(i, hkaPose.PropagateOrNot.DontPropagate);
                if (model == null) continue;
                var local = new Vector3(model->Translation.X, model->Translation.Y, model->Translation.Z);
                bones[name] = position + Vector3.Transform(local * scale, rotation);
            }
        }
        return bones.Count == 0 ? null : (bones, MathF.Atan2(forward.X, forward.Z));
    }

    /// The average of the named bones present, or null when none are.
    public static Vector3? Average(IReadOnlyDictionary<string, Vector3> bones, IReadOnlyList<string> names)
    {
        var sum = Vector3.Zero;
        var count = 0;
        foreach (var name in names)
        {
            if (!bones.TryGetValue(name, out var at)) continue;
            sum += at;
            count++;
        }
        return count == 0 ? null : sum / count;
    }
}
