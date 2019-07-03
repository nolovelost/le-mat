using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;
using UnityEditor;

public class AnimationClipGraphManager
{
    private static AnimationClipGraphManager animationClipGraphManager;

    public static AnimationClipGraphManager instance
    {
        get
        {
            if (animationClipGraphManager == null)
                animationClipGraphManager = new AnimationClipGraphManager();
            return animationClipGraphManager;
        }
    }

    public Dictionary<AnimationClipPlayable, AnimationClipNode> playableNodes = new Dictionary<AnimationClipPlayable, AnimationClipNode>();

    public void RegisterNode(AnimationClipPlayable playable, AnimationClipNode node)
    {
        playableNodes.Add(playable, node);
    }
}
