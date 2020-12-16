using FixedMaths;
using Svelto.ECS;
using Svelto.ECS.Schedulers;
using MiniExamples.DeterministicPhysicDemo.Graphics;
using MiniExamples.DeterministicPhysicDemo.Physics;
using MiniExamples.DeterministicPhysicDemo.Physics.Builders;
using MiniExamples.DeterministicPhysicDemo.Physics.Engines;

namespace MiniExamples.DeterministicPhysicDemo
{
    public class CompositionRoot
    {
        public CompositionRoot(IGraphics graphics)
        {
            _graphics          = graphics;
            _schedulerReporter = new EngineSchedulerReporter();
            _scheduler         = new EngineScheduler(_schedulerReporter);
            
            _simpleSubmissionEntityViewScheduler = new SimpleEntitiesSubmissionScheduler();
            var enginesRoot = new EnginesRoot(_simpleSubmissionEntityViewScheduler);

            enginesRoot.AddEngine(new DebugPhysicsDrawEngine(_scheduler, _graphics));
            PhysicsCore.RegisterTo(enginesRoot, _scheduler);

            AddEntities(enginesRoot.GenerateEntityFactory(), _simpleSubmissionEntityViewScheduler);
        }
        public IEngineSchedulerReporter schedulerReporter => _schedulerReporter;
        public IEngineScheduler scheduler => _scheduler;

        static void AddEntities(IEntityFactory entityFactory, SimpleEntitiesSubmissionScheduler simpleSubmissionEntityViewScheduler)
        {
            // Make a simple bounding box
            RigidBodyWithColliderBuilder.Create()
                                        .SetPosition(FixedPointVector2.From(FixedPoint.From(0), FixedPoint.From(-100)))
                                        .SetBoxCollider(FixedPointVector2.From(100, 5))
                                        .SetIsKinematic(true)
                                        .Build(entityFactory, 0);
            
            RigidBodyWithColliderBuilder.Create()
                                        .SetPosition(FixedPointVector2.From(FixedPoint.From(0), FixedPoint.From(100)))
                                        .SetBoxCollider(FixedPointVector2.From(100, 5))
                                        .SetIsKinematic(true)
                                        .Build(entityFactory, 1);
            
            RigidBodyWithColliderBuilder.Create()
                                        .SetPosition(FixedPointVector2.From(FixedPoint.From(-100), FixedPoint.From(0)))
                                        .SetBoxCollider(FixedPointVector2.From(5, 100))
                                        .SetIsKinematic(true)
                                        .Build(entityFactory, 2);
            
            RigidBodyWithColliderBuilder.Create()
                                        .SetPosition(FixedPointVector2.From(FixedPoint.From(100), FixedPoint.From(0)))
                                        .SetBoxCollider(FixedPointVector2.From(5, 100))
                                        .SetIsKinematic(true)
                                        .Build(entityFactory, 3);
            
            // Add some bounding boxes
            AddBoxColliderEntity(entityFactory, 4, FixedPointVector2.From(FixedPoint.From(-30), FixedPoint.From(0)), FixedPointVector2.Down, FixedPoint.From(3), FixedPointVector2.From(10, 10));
            AddBoxColliderEntity(entityFactory, 5, FixedPointVector2.From(FixedPoint.From(-35), FixedPoint.From(-50)), FixedPointVector2.Up, FixedPoint.From(5), FixedPointVector2.From(10, 10));
            AddBoxColliderEntity(entityFactory, 6, FixedPointVector2.From(FixedPoint.From(-30), FixedPoint.From(50)), FixedPointVector2.Up, FixedPoint.From(3), FixedPointVector2.From(10, 10));
            AddBoxColliderEntity(entityFactory, 7, FixedPointVector2.From(FixedPoint.From(0), FixedPoint.From(50)), FixedPointVector2.Right, FixedPoint.From(3), FixedPointVector2.From(10, 10));
            AddBoxColliderEntity(entityFactory, 8, FixedPointVector2.From(FixedPoint.From(40), FixedPoint.From(-90)), FixedPointVector2.From(1, 1).Normalize(), FixedPoint.From(10), FixedPointVector2.From(3, 3));
            AddBoxColliderEntity(entityFactory, 9, FixedPointVector2.From(FixedPoint.From(40), FixedPoint.From(-60)), FixedPointVector2.From(1, 1).Normalize(), FixedPoint.From(10), FixedPointVector2.From(3, 3));
            AddBoxColliderEntity(entityFactory, 10, FixedPointVector2.From(FixedPoint.From(40), FixedPoint.From(-30)), FixedPointVector2.From(1, 1).Normalize(), FixedPoint.From(3), FixedPointVector2.From(3, 3));
            
            //this is wrong, SubmitEntities() must happen every tick, not just once, otherwise new entities cannot be submitted
            simpleSubmissionEntityViewScheduler.SubmitEntities();
        }
        
        private static void AddBoxColliderEntity(IEntityFactory entityFactory, uint egid, FixedPointVector2 position, FixedPointVector2 direction, FixedPoint speed, FixedPointVector2 boxColliderSize)
        {
            RigidBodyWithColliderBuilder.Create()
                                        .SetPosition(position)
                                        .SetDirection(direction)
                                        .SetSpeed(speed)
                                        .SetBoxCollider(boxColliderSize)
                                        .Build(entityFactory, egid);
        }

        readonly EngineScheduler          _scheduler;
        readonly EngineSchedulerReporter  _schedulerReporter;
        readonly IGraphics                _graphics;
        
        SimpleEntitiesSubmissionScheduler _simpleSubmissionEntityViewScheduler;
    }
}