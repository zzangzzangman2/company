using System;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Resolves one runtime displacement against the canonical occupancy model. Contact refinement
    /// makes the stopping boundary independent of render-frame partitioning while the remaining
    /// displacement keeps the existing deterministic axis-slide preference.
    /// </summary>
    public static class OfficeRuntimeCollisionMotion
    {
        private const int ContactRefinementIterations = 8;
        private const float MinimumDisplacementSquared = 0.0000001f;

        public static Vector2 Resolve(
            OfficeRuntimeOccupancy occupancy,
            string agentId,
            Vector2 start,
            Vector2 intendedDisplacement,
            Vector2 semanticVelocity,
            Vector2 previousDisplacement,
            float radius,
            string permittedSeatId,
            out bool collisionProjected)
        {
            return Resolve(
                occupancy,
                agentId,
                start,
                intendedDisplacement,
                semanticVelocity,
                previousDisplacement,
                radius,
                permittedSeatId,
                out collisionProjected,
                out _);
        }

        public static Vector2 Resolve(
            OfficeRuntimeOccupancy occupancy,
            string agentId,
            Vector2 start,
            Vector2 intendedDisplacement,
            Vector2 semanticVelocity,
            Vector2 previousDisplacement,
            float radius,
            string permittedSeatId,
            out bool collisionProjected,
            out Vector2 contactDisplacement)
        {
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (intendedDisplacement.sqrMagnitude <= MinimumDisplacementSquared)
            {
                collisionProjected = false;
                contactDisplacement = Vector2.zero;
                return Vector2.zero;
            }
            if (occupancy.CanMove(
                    agentId,
                    start,
                    start + intendedDisplacement,
                    radius,
                    permittedSeatId))
            {
                collisionProjected = false;
                contactDisplacement = intendedDisplacement;
                return intendedDisplacement;
            }

            Vector2 contact = RefineSafeDisplacement(
                occupancy,
                agentId,
                start,
                intendedDisplacement,
                radius,
                permittedSeatId);
            contactDisplacement = contact;
            Vector2 contactPosition = start + contact;
            Vector2 remaining = intendedDisplacement - contact;
            Vector2 safeX = RefineSafeDisplacement(
                occupancy,
                agentId,
                contactPosition,
                new Vector2(remaining.x, 0f),
                radius,
                permittedSeatId);
            Vector2 safeY = RefineSafeDisplacement(
                occupancy,
                agentId,
                contactPosition,
                new Vector2(0f, remaining.y),
                radius,
                permittedSeatId);
            bool canMoveX = Mathf.Abs(safeX.x) > 0.00001f;
            bool canMoveY = Mathf.Abs(safeY.y) > 0.00001f;
            OfficeNavPoint slide = OfficeCollisionSlideRules.SelectBestAxisSlide(
                new OfficeNavPoint(safeX.x, safeY.y),
                new OfficeNavPoint(semanticVelocity.x, semanticVelocity.y),
                new OfficeNavPoint(previousDisplacement.x, previousDisplacement.y),
                canMoveX,
                canMoveY,
                agentId);
            Vector2 actual = contact + new Vector2(slide.X, slide.Z);
            collisionProjected = (actual - intendedDisplacement).sqrMagnitude > MinimumDisplacementSquared;
            return actual;
        }

        private static Vector2 RefineSafeDisplacement(
            OfficeRuntimeOccupancy occupancy,
            string agentId,
            Vector2 start,
            Vector2 displacement,
            float radius,
            string permittedSeatId)
        {
            if (displacement.sqrMagnitude <= MinimumDisplacementSquared) return Vector2.zero;
            if (occupancy.CanMove(agentId, start, start + displacement, radius, permittedSeatId))
                return displacement;

            float safe = 0f;
            float blocked = 1f;
            for (var iteration = 0; iteration < ContactRefinementIterations; iteration++)
            {
                float probe = (safe + blocked) * 0.5f;
                if (occupancy.CanMove(
                        agentId,
                        start,
                        start + displacement * probe,
                        radius,
                        permittedSeatId)) safe = probe;
                else blocked = probe;
            }
            return displacement * safe;
        }
    }
}
