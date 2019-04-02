using Svelto.Context;
using Svelto.ECS.Example.Survive.Camera;
using Svelto.ECS.Example.Survive.Characters;
using Svelto.ECS.Example.Survive.Characters.Enemies;
using Svelto.ECS.Example.Survive.Characters.Player;
using Svelto.ECS.Example.Survive.Characters.Player.Gun;
using Svelto.ECS.Example.Survive.Characters.Sounds;
using Svelto.ECS.Example.Survive.HUD;
using Svelto.ECS.Example.Survive.ResourceManager;
using Svelto.ECS.Schedulers.Unity;
using Svelto.Tasks;
using UnityEngine;

//Main is the Application Composition Root. A Composition Root is the where all the dependencies are 
//created and injected (I talk a lot about this in my articles) A composition root belongs to the Context, but
//a context can have more than a composition root. For example a factory is a composition root.
//Furthermore an application can have more than a context but this is more advanced and not part of this demo
namespace Svelto.ECS.Example.Survive
{
    /// <summary>
    ///     IComposition root is part of Svelto.Context. Svelto.Context is not formally part of Svelto.ECS, but
    ///     it's helpful to use in an environment where a Context is not present, like in Unity.
    ///     It's a bootstrap!
    /// </summary>
    public class Main : ICompositionRoot
    {
        EnginesRoot                    _enginesRoot;
        IEntityFactory                 _entityFactory;
        UnityEntitySubmissionScheduler _unityEntitySubmissionScheduler;

        public Main()
        {
            QualitySettings.vSyncCount = 1;
            
            SetupEngines();
            SetupEntities();
        }

        public void OnContextCreated<T>(T contextHolder) { BuildEntitiesFromScene(contextHolder as UnityContext); }
        public void OnContextInitialized<T>(T contextHolder) { }
        public void OnContextDestroyed()
        {
            //final clean up
            _enginesRoot.Dispose();

            //Tasks can run across level loading, so if you don't want that, the runners must be stopped explicitly.
            //careful because if you don't do it and unintentionally leave tasks running, you will cause leaks
            TaskRunner.StopAndCleanupAllDefaultSchedulers();
        }

        /// <summary>
        ///     Before to start, let's review some of the Svelto.ECS terms:
        ///     - Entity:
        ///     it must be a real and concrete entity that you can explain in terms of game design. The name of each
        ///     entity should reflect a specific concept from the game design domain
        ///     - Engines (Systems):
        ///     Where all the logic lies. Engines operates on EntityViewStructs and EntityStructs
        ///     - EntityStructs:
        ///     EntityStructs is the preferred way to store entity data. They are just plain structs of pure data (no
        ///     objects)
        ///     - EntityViewStructs:
        ///     EntityViewStructs are used to wrap Objects that come from OOP libraries. You will never use it unless
        ///     you are forced to mix your ECS code with OOP code because of external libraries or platforms.
        ///     The Objects are known to svelto through Component Interfaces. 
        ///     - Component Interfaces:
        ///     Components must be seen as data holders. In Svelto.ECS components are always interfaces declaring
        ///     Setters and Getters of Value Types coming from the Objects they wrap 
        ///     - Implementors:
        ///     The components interfaces must be implemented through Implementors and the implementors are the
        ///     Objects you need to wrap.
        ///     - EntityDescriptors:
        ///     Gives a way to formalise your Entity, it also defines the EntityStructs and EntityViewStructs that must
        ///     be generated once the Entity is built
        /// </summary>
        void SetupEngines()
        {
            //The Engines Root is the core of Svelto.ECS. You must NEVER inject the EngineRoot
            //as it is, therefore the composition root must hold a reference or it will be 
            //GCed.
            //the UnitySumbmissionEntityViewScheduler is the scheduler that is used by the EnginesRoot to know
            //when to inject the EntityViews. You shouldn't use a custom one unless you know what you 
            //are doing or you are not working with Unity.
            _unityEntitySubmissionScheduler = new UnityEntitySubmissionScheduler();
            _enginesRoot                    = new EnginesRoot(_unityEntitySubmissionScheduler);
            //Engines root can never be held by anything else than the context itself to avoid leaks
            //That's why the EntityFactory and EntityFunctions are generated.
            //The EntityFactory can be injected inside factories (or engine acting as factories)
            //to build new entities dynamically
            _entityFactory = _enginesRoot.GenerateEntityFactory();
            //The entity functions is a set of utility operations on Entities, including
            //removing an entity. I couldn't find a better name so far.
            var entityFunctions = _enginesRoot.GenerateEntityFunctions();

            //the ISequencer is one of the 2 official ways available in Svelto.ECS 
            //to communicate. They are mainly used for two specific cases:
            //1) specify a strict execution order between engines (engine logic
            //is executed horizontally instead than vertically, I will talk about this
            //in my articles). 2) filter a data token passed as parameter through
            //engines. The ISequencer is also not the common way to communicate
            //between engines
            var playerDeathSequence = new PlayerDeathSequencer();
            var enemyDeathSequence  = new EnemyDeathSequencer();

            //wrap non testable unity static classes, so that 
            //can be mocked if needed.
            IRayCaster rayCaster = new RayCaster();
            ITime      time      = new Time();

            //Player related engines. ALL the dependencies must be solved at this point
            //through constructor injection.
            var playerShootingEngine  = new PlayerGunShootingEngine(rayCaster, time);
            var playerMovementEngine  = new PlayerMovementEngine(rayCaster, time);
            var playerAnimationEngine = new PlayerAnimationEngine();
            var playerDeathEngine     = new PlayerDeathEngine(playerDeathSequence, entityFunctions);

            //Enemy related engines
            var enemyAnimationEngine = new EnemyAnimationEngine(time, enemyDeathSequence, entityFunctions);
            var enemyAttackEngine    = new EnemyAttackEngine(time);
            var enemyMovementEngine  = new EnemyMovementEngine();

            //GameObjectFactory allows to create GameObjects without using the Static
            //method GameObject.Instantiate. While it seems a complication
            //it's important to keep the engines testable and not
            //coupled with hard dependencies references (read my articles to understand
            //how dependency injection works and why solving dependencies
            //with static classes and singletons is a terrible mistake)
            var gameObjectFactory = new GameObjectFactory();
            //Factory is one of the few patterns that work very well with ECS. Its use is highly encouraged
            var enemyFactory       = new EnemyFactory(gameObjectFactory, _entityFactory);
            var enemySpawnerEngine = new EnemySpawnerEngine(enemyFactory, entityFunctions);
            var enemyDeathEngine   = new EnemyDeathEngine(entityFunctions, enemyDeathSequence);

            //hud and sound engines
            var hudEngine         = new HUDEngine(time);
            var damageSoundEngine = new DamageSoundEngine();
            var scoreEngine       = new ScoreEngine();

            //The ISequencer implementation is very simple, but allows to perform
            //complex concatenation including loops and conditional branching.
            //These two sequencers are a real stretch and are shown only for explanatory purposes. 
            //Please do not see sequencers as a way to dispatch or broadcast events, they are meant only and exclusively
            //to guarantee the order of execution of the involved engines.
            //For this reason the use of sequencers is and must be actually rare, as perfectly encapsulated engines
            //do not need to be executed in specific order.
            //a Sequencer can: 
            //- ensure the order of execution through one step only (one step executes in order several engines)
            //- ensure the order of execution through several steps. Each engine inside each step has the responsibility
            //to trigger the next step through the use of the Next() function
            //- create paths with branches and loop using the Condition parameter.
            playerDeathSequence.SetSequence(playerDeathEngine, playerMovementEngine, playerAnimationEngine,
                                            enemyAnimationEngine, damageSoundEngine, hudEngine);

            enemyDeathSequence.SetSequence(enemyDeathEngine, scoreEngine, damageSoundEngine, enemyAnimationEngine,
                                           enemySpawnerEngine);


            //All the logic of the game must lie inside engines
            //Player engines
            _enginesRoot.AddEngine(playerMovementEngine);
            _enginesRoot.AddEngine(playerAnimationEngine);
            _enginesRoot.AddEngine(playerShootingEngine);
            _enginesRoot.AddEngine(new PlayerInputEngine());
            _enginesRoot.AddEngine(new PlayerGunShootingFXsEngine());
            _enginesRoot.AddEngine(playerDeathEngine);

            //enemy engines
            _enginesRoot.AddEngine(enemySpawnerEngine);
            _enginesRoot.AddEngine(enemyAttackEngine);
            _enginesRoot.AddEngine(enemyMovementEngine);
            _enginesRoot.AddEngine(enemyAnimationEngine);
            _enginesRoot.AddEngine(enemyDeathEngine);
            //other engines
            _enginesRoot.AddEngine(new ApplyingDamageToTargetsEngine());
            _enginesRoot.AddEngine(new CameraFollowTargetEngine(time));
            _enginesRoot.AddEngine(new CharactersDeathEngine());
            _enginesRoot.AddEngine(damageSoundEngine);
            _enginesRoot.AddEngine(hudEngine);
            _enginesRoot.AddEngine(scoreEngine);
        }


        /// <summary>
        ///     While until recently I thought that creating entities in the context would be all right, I am coming to
        ///     realise that engines should always handle the creation of entities. I will refactor this when the right
        ///     time comes.
        /// </summary>
        void SetupEntities()
        {
            BuildPlayerEntities();
            BuildCameraEntity();
        }

        void BuildPlayerEntities()
        {
            var prefabsDictionary = new PrefabsDictionary();

            var player = prefabsDictionary.Istantiate("Player");

            //Initialize an entity inside a composition root is a so-so practice, better to have an engineSpawner.
            var initializer =
                _entityFactory.BuildEntity<PlayerEntityDescriptor>((uint) player.GetInstanceID(), ECSGroups.Player,
                                                                   player.GetComponents<IImplementor>());
            initializer.Init(new HealthEntityStruct {currentHealth = 100});

            //unluckily the gun is parented in the original prefab, so there is no easy way to create it explicitly, I
            //have to create if from the existing gameobject.
            var gunImplementor = player.GetComponentInChildren<PlayerShootingImplementor>();

            _entityFactory.BuildEntity<PlayerGunEntityDescriptor>((uint) gunImplementor.gameObject.GetInstanceID(),
                                                                  ECSGroups.Player, new[] {gunImplementor});
        }

        void BuildCameraEntity()
        {
            var implementor = UnityEngine.Camera.main.gameObject.AddComponent<CameraImplementor>();

            _entityFactory.BuildEntity<CameraEntityDescriptor>((uint) UnityEngine.Camera.main.GetInstanceID(),
                                                               ECSGroups.ExtraStuff, new[] {implementor});
        }

        /// <summary>
        ///     This is a possible approach to create Entities from already existing GameObject in the scene
        ///     It is absolutely not necessary and I wouldn't rely on this in production
        /// </summary>
        /// <param name="contextHolder"></param>
        void BuildEntitiesFromScene(UnityContext contextHolder)
        {
            //An EntityDescriptorHolder is a special Svelto.ECS class created to exploit
            //GameObjects to dynamically retrieve the Entity information attached to it.
            //Basically a GameObject can be used to hold all the information needed to create
            //an Entity and later queries to build the entitity itself.
            //This allows to trigger a sort of polymorphic code that can be re-used to 
            //create several type of entities.

            var entities = contextHolder.GetComponentsInChildren<IEntityDescriptorHolder>();

            //However this common pattern in Svelto.ECS application exists to automatically
            //create entities from gameobjects already presented in the scene.
            //I still suggest to avoid this method though and create entities always
            //manually and explicitly. Basically EntityDescriptorHolder should be avoided
            //whenever not strictly necessary.

            for (var i = 0; i < entities.Length; i++)
            {
                var entityDescriptorHolder = entities[i];
                var entityViewsToBuild     = entityDescriptorHolder.GetDescriptor();
                _entityFactory
                   .BuildEntity(new EGID((uint) ((MonoBehaviour) entityDescriptorHolder).gameObject.GetInstanceID(), ECSGroups.ExtraStuff),
                                entityViewsToBuild,
                                (entityDescriptorHolder as MonoBehaviour).GetComponentsInChildren<IImplementor>());
            }
        }
    }

    /// <summary>
    ///     At least One GameObject containing a UnityContext must be present in the scene.
    ///     All the monobehaviours existing in gameobjects child of the UnityContext one,
    ///     can be later queried, usually to create entities from statically created
    ///     gameobjects.
    /// </summary>
    public class MainContext : UnityContext<Main>
    {
    }
}