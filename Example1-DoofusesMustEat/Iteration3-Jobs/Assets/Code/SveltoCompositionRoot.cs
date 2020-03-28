using System;
using System.Collections;
using Svelto.Context;
using Svelto.DataStructures;
using Svelto.ECS.Extensions.Unity;
using Svelto.ECS.Internal;
using Svelto.Tasks;
using Svelto.Tasks.ExtraLean;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Svelto.ECS.MiniExamples.Example1C
{
    public class SveltoCompositionRoot : ICompositionRoot, ICustomBootstrap
    {
        static World _world;

        EnginesRoot _enginesRoot;
        FasterList<IJobifiableEngine> _enginesToTick;
        SimpleSubmissioncheduler _simpleSubmitScheduler;

        void StartTicking(FasterList<IJobifiableEngine> engines)
        {
            MainThreadTick(engines).RunOn(DoofusesStandardSchedulers.mainThreadScheduler);
        }

        IEnumerator MainThreadTick(FasterList<IJobifiableEngine> engines)
        {
            EnginesExecutionOrder order = new EnginesExecutionOrder(new FasterReadOnlyList<IJobifiableEngine>(engines));

            JobHandle jobs = default;
            
            while (true)
            {
                jobs.Complete();
                
                //Sync point on the mainthread:
                //Svelto entities are added/removed/swapped
                //callback functions are called (which may create UECS entities)
                _simpleSubmitScheduler.SubmitEntities();

                //schedule all jobs and let them run until next frame;
                order.Execute(jobs);
                
                yield return Yield.It;
            }
        }

        public void OnContextInitialized<T>(T contextHolder)
        { }

        void AddSveltoCallbackEngine(IReactEngine engine)
        {
            _enginesRoot.AddEngine(engine);
        }

        void AddSveltoEngineToTick(IJobifiableEngine engine)
        {
            _enginesRoot.AddEngine(engine);
            _enginesToTick.Add(engine);
        }

        void AddSveltoUECSEngine<T>(T engine) where T : ComponentSystemBase, ICopySveltoToUECSEngine
        {
            //it's a Svelto Engine/UECS SystemBase so it must be added in the UECS world AND svelto enginesRoot
            _world.AddSystem(engine);
            _enginesRoot.AddEngine(engine);
            
            //We assume that the UECS/Svelto engines are to be added in teh SimulationSystemGroup
            var copySveltoToUecsEnginesGroup = _world.GetExistingSystem<CopySveltoToUECSEnginesGroup>();
            //Svelto will tick the UECS group that will tick the System, this because we still rely on the UECS
            //dependency tracking for the UECS components too
            copySveltoToUecsEnginesGroup.AddSystemToUpdateList(engine);
        }

        public void OnContextDestroyed()
        {
            DoofusesStandardSchedulers.StopAndCleanupAllDefaultSchedulers();
            
            GC.Collect();
            GC.WaitForPendingFinalizers();

            _enginesRoot?.Dispose();
        }

        public void OnContextCreated<T>(T contextHolder) { }

        public bool Initialize(string defaultWorldName)
        {
            //            Physics.autoSimulation = false;
            QualitySettings.vSyncCount = -1;

            _simpleSubmitScheduler = new SimpleSubmissioncheduler();
            _enginesRoot = new EnginesRoot(_simpleSubmitScheduler);
            _enginesToTick = new FasterList<IJobifiableEngine>();

            _world = new World("Custom world");

            World.DefaultGameObjectInjectionWorld = _world;
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world, systems);

            var copySveltoToUecsEnginesGroup = new CopySveltoToUECSEnginesGroup();
            _world.AddSystem(copySveltoToUecsEnginesGroup);
            AddSveltoEngineToTick(copySveltoToUecsEnginesGroup);
            
            var tickUECSSystemsGroup = new PureUECSSystemsGroup(_world);
            AddSveltoEngineToTick(tickUECSSystemsGroup);
            
            ///Svelto will tick the UECS engines, We need control over everything
            //ScriptBehaviourUpdateOrder.UpdatePlayerLoop(_world);
            
            //add the engines we are going to use
            var generateEntityFactory = _enginesRoot.GenerateEntityFactory();

            var redfoodEntity =
                GameObjectConversionUtility.ConvertGameObjectHierarchy(Resources.Load("Sphere") as GameObject,
                                                                       new GameObjectConversionSettings()
                                                                           {DestinationWorld = _world});
            var bluefoodEntity =
                GameObjectConversionUtility.ConvertGameObjectHierarchy(Resources.Load("Sphereblue") as GameObject,
                                                                       new GameObjectConversionSettings()
                                                                           {DestinationWorld = _world});
            var redDoofusEntity =
                GameObjectConversionUtility.ConvertGameObjectHierarchy(Resources.Load("RedCapsule") as GameObject,
                                                                       new GameObjectConversionSettings()
                                                                           {DestinationWorld = _world});
            var blueDoofusEntity =
                GameObjectConversionUtility.ConvertGameObjectHierarchy(Resources.Load("BlueCapsule") as GameObject,
                                                                       new GameObjectConversionSettings()
                                                                           {DestinationWorld = _world});

            AddSveltoEngineToTick(new PlaceFoodOnClickEngine(redfoodEntity, bluefoodEntity, generateEntityFactory));
            AddSveltoEngineToTick(new SpawningDoofusEngine(redDoofusEntity, blueDoofusEntity, generateEntityFactory));
            AddSveltoEngineToTick(new ConsumingFoodEngine(_enginesRoot.GenerateEntityFunctions()));
            AddSveltoEngineToTick(new LookingForFoodDoofusesEngine());
            AddSveltoEngineToTick(new VelocityToPositionDoofusesEngine());
            
            AddSveltoCallbackEngine(new SpawnUnityEntityOnSveltoEntityEngine(_world));
            
            AddSveltoUECSEngine(new RenderingUECSDataSynchronizationEngine());
            
            StartTicking(_enginesToTick);

            return true;
        }
    }
}