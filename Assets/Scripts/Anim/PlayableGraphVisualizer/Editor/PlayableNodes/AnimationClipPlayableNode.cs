using System.Text;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GraphVisualizer
{
    public class AnimationClipPlayableNode : PlayableNode
    {
        public AnimationClipPlayableNode(Playable content, float weight = 1.0f)
            : base(content, weight)
        {
            AnimationClipGraphManager.instance.playableInfo.TryGetValue((AnimationClipPlayable)content, out base.playableInfo);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(base.ToString());

            var p = (Playable) content;
            if (p.IsValid())
            {
                var acp = (AnimationClipPlayable) p;
                var clip = acp.GetAnimationClip();
                sb.AppendLine(InfoString("Clip", clip ? clip.name : "(none)"));
                if (clip)
                {
                    sb.AppendLine(InfoString("ClipLength", clip.length));
                }
                sb.AppendLine(InfoString("ApplyFootIK", acp.GetApplyFootIK()));
                sb.AppendLine(InfoString("ApplyPlayableIK", acp.GetApplyPlayableIK()));

                AnimationClipInfo infoNode;
                AnimationClipGraphManager.instance.playableInfo.TryGetValue(acp, out infoNode);
                sb.AppendLine(InfoString("Clip Name:", infoNode.clipName != null ? infoNode.clipName : "NA"));
            }

            return sb.ToString();
        }
    }
}