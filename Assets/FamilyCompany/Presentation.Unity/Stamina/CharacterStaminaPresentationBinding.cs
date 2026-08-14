using System;
using FamilyCompany.Simulation.Stamina;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    /// <summary>
    /// Transient presentation bridge. It never owns stamina state; the GameState integration owner
    /// binds its read model and can replace it atomically after new game or load.
    /// </summary>
    public static class CharacterStaminaPresentationBinding
    {
        private static UnityEngine.Object _owner;
        private static ICharacterStaminaReadModel _model;
        private static int _revision;

        public static int Revision => _revision;

        public static void Bind(
            UnityEngine.Object owner,
            ICharacterStaminaReadModel readModel)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (readModel == null) throw new ArgumentNullException(nameof(readModel));
            if (_owner == owner && ReferenceEquals(_model, readModel)) return;
            _owner = owner;
            _model = readModel;
            IncrementRevision();
        }

        public static void Unbind(UnityEngine.Object owner)
        {
            if (_owner == null)
            {
                ClearWithoutOwnerCheck();
                return;
            }
            if (owner != _owner) return;
            ClearWithoutOwnerCheck();
        }

        public static bool TryGet(
            out ICharacterStaminaReadModel readModel,
            out int revision)
        {
            if (_owner == null || _model == null)
            {
                if (!ReferenceEquals(_owner, null) || _model != null)
                    ClearWithoutOwnerCheck();
                readModel = null;
                revision = _revision;
                return false;
            }
            readModel = _model;
            revision = _revision;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            _owner = null;
            _model = null;
            _revision = 0;
        }

        private static void ClearWithoutOwnerCheck()
        {
            if (ReferenceEquals(_owner, null) && _model == null) return;
            _owner = null;
            _model = null;
            IncrementRevision();
        }

        private static void IncrementRevision()
        {
            _revision = _revision == int.MaxValue ? 1 : _revision + 1;
        }
    }
}
