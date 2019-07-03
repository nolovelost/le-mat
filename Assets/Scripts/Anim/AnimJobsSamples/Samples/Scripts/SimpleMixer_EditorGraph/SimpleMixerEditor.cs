using Unity.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;

[ExecuteInEditMode]
public class SimpleMixerEditor : MonoBehaviour
{
    [Range(0.0f, 1.0f)]
    public float weight;

    NativeArray<TransformStreamHandle> m_Handles;
    NativeArray<float> m_BoneWeights;

    PlayableGraph m_Graph;
    AnimationScriptPlayable m_CustomMixerPlayable;

    PlayableGraph emptyGraph;

    void OnEnable()
    {
        // Load animation clips.
        var idleClip = SampleUtility.LoadAnimationClipFromFbx("DefaultMale/Models/DefaultMale_Generic", "Idle");
        var romClip = SampleUtility.LoadAnimationClipFromFbx("DefaultMale/Models/DefaultMale_Generic", "ROM");
        if (idleClip == null || romClip == null)
            return;

        var animator = GetComponent<Animator>();

        // Get all the transforms in the hierarchy.
        var transforms = animator.transform.GetComponentsInChildren<Transform>();
        var numTransforms = transforms.Length - 1;

        // Fill native arrays (all the bones have a weight of 1.0).
        m_Handles = new NativeArray<TransformStreamHandle>(numTransforms, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        m_BoneWeights = new NativeArray<float>(numTransforms, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        for (var i = 0; i < numTransforms; ++i)
        {
            m_Handles[i] = animator.BindStreamTransform(transforms[i + 1]);
            m_BoneWeights[i] = 1.0f;
        }

        // Create job.
        var job = new MixerJob()
        {
            handles = m_Handles,
            boneWeights = m_BoneWeights,
            weight = 0.0f
        };

        // Create graph with custom mixer.
        m_Graph = PlayableGraph.Create("SimpleMixer");
        m_Graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        m_CustomMixerPlayable = AnimationScriptPlayable.Create(m_Graph, job);
        m_CustomMixerPlayable.SetProcessInputs(false);

        AnimationClipPlayable idlePlayable = CreateAnimationPlayableClip(m_Graph, idleClip);
        AnimationClipPlayable romPlayable = CreateAnimationPlayableClip(m_Graph, romClip);

        m_CustomMixerPlayable.AddInput(idlePlayable, 0, 1.0f);
        m_CustomMixerPlayable.AddInput(romPlayable, 0, 1.0f);

        var output = AnimationPlayableOutput.Create(m_Graph, "output", animator);
        output.SetSourcePlayable(m_CustomMixerPlayable);

        m_Graph.Play();

        emptyGraph = PlayableGraph.Create("EmptyGraph");
        emptyGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
    }

    AnimationClipPlayable CreateAnimationPlayableClip(PlayableGraph graph, AnimationClip clip)
    {
        AnimationClipPlayable playableClip = AnimationClipPlayable.Create(graph, clip);
        AnimationClipInfo info = new AnimationClipInfo();
        info = new AnimationClipInfo();
        info.clipName = clip.name;
        AnimationClipGraphManager.instance.RegisterNode(playableClip, info);
        return playableClip;
    }

    AnimationClipPlayable CreateAnimationPlayableClip(PlayableGraph graph, AnimationClip clip, AnimationClipInfo info)
    {
        AnimationClipPlayable playableClip = AnimationClipPlayable.Create(graph, clip);
        AnimationClipGraphManager.instance.RegisterNode(playableClip, info);
        return playableClip;
    }

    void Update()
    {
        var job = m_CustomMixerPlayable.GetJobData<MixerJob>();

        job.weight = weight;

        m_CustomMixerPlayable.SetJobData(job);
    }

    [ContextMenu("ResetAnim")]
    void ResetAnim()
    {
        m_Graph.Destroy();
        m_Handles.Dispose();
        m_BoneWeights.Dispose();
        OnEnable();
    }

    void OnDisable()
    {
        m_Graph.Destroy();
        emptyGraph.Destroy();
        m_Handles.Dispose();
        m_BoneWeights.Dispose();
    }
}
