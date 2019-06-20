using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;

public class GameWorld
{
    public GameTime worldTime;
    public float lastWorldTick;

    public float frameDuration;
    public double nextTickTime;

    [ReadOnly] public GameObject sceneRoot;

    EntityManager entityManager;
    World world;

    public static List<GameWorld> worlds = new List<GameWorld>();

    public GameWorld(string name = "world")
    {
        GameDebug.Log("GameWorld " + name + " initializing");

        sceneRoot = new GameObject(name);
        GameObject.DontDestroyOnLoad(sceneRoot);

        GameDebug.Assert(World.Active != null, "There is no active world");
        world = World.Active;
        entityManager = world.EntityManager;
        GameDebug.Assert(entityManager.IsCreated, "EntityManager hasn't been created");

        worldTime.tickRate = 60;
        nextTickTime = Game.frameTime;

        worlds.Add(this);

        //m_destroyDespawningSystem = m_ECSWorld.CreateManager<DestroyDespawning>();
    }

    public void Shutdown()
    {
        GameDebug.Log("GameWorld " + world.Name + " shutting down");

        // Destroy functionalities and objects here...

        worlds.Remove(this);
        GameObject.Destroy(sceneRoot);
    }

    public EntityManager GetEntityManager()
    {
        return entityManager;
    }

    public World GetECSWorld()
    {
        return world;
    }
}
