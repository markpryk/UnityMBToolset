using UnityEngine;
using System.Collections.Generic;

namespace WarbandParticles
{
    /// <summary>
    /// Simple non-instanced particle renderer that fallbacks to drawing regular meshes
    /// </summary>
    public class WarbandParticleFallbackRenderer
    {
        private WarbandParticleSystem _particleSystem;
        private Mesh _particleMesh;
        private Material _material;
        
        // List of matrices for standard mesh drawing
        private List<Matrix4x4> _matrices = new List<Matrix4x4>();
        private List<Color> _colors = new List<Color>();
        private MaterialPropertyBlock _propertyBlock;
        
        // Maximum number of particles to render in a single batch
        private const int MAX_BATCH_SIZE = 1023; // Unity limitation for non-instanced batches
        
        public WarbandParticleFallbackRenderer(WarbandParticleSystem particleSystem)
        {
            _particleSystem = particleSystem;
            _particleMesh = particleSystem.ParticleMesh;
            _material = particleSystem.Material;
            _propertyBlock = new MaterialPropertyBlock();
        }
        
        public void Render(List<Particle> particles, Camera camera)
        {
            if (_particleMesh == null || _material == null || particles.Count == 0)
                return;
                
            // Get camera data
            Vector3 cameraPosition = camera.transform.position;
            
            // Prepare matrices and colors
            _matrices.Clear();
            _colors.Clear();
            
            // Calculate matrices for each particle
            for (int i = 0; i < particles.Count; i++)
            {
                Matrix4x4 matrix = CalculateParticleMatrix(particles[i], cameraPosition);
                Color color = _particleSystem.GetColorAtTime(particles[i].Time);
                
                _matrices.Add(matrix);
                _colors.Add(color);
                
                // Draw in batches if we reach max batch size
                if (_matrices.Count >= MAX_BATCH_SIZE || i == particles.Count - 1)
                {
                    DrawParticleBatch();
                }
            }
        }
        
        private Matrix4x4 CalculateParticleMatrix(Particle particle, Vector3 cameraPosition)
        {
            // Calculate billboard orientation based on flags
            Vector3 right, up, forward;
            Vector3 toCamera = (cameraPosition - particle.Position).normalized;
            
            // Handle billboard modes
            if ((_particleSystem.Flags & ParticleSystemFlags.Billboard2D) != 0)
            {
                up = Vector3.up;
                forward = toCamera;
                right = Vector3.Cross(up, forward).normalized;
                forward = Vector3.Cross(right, up);
            }
            else if ((_particleSystem.Flags & ParticleSystemFlags.Billboard3D) != 0)
            {
                forward = toCamera;
                up = Vector3.up;
                right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right);
            }
            else if ((_particleSystem.Flags & ParticleSystemFlags.BillboardDrop) != 0)
            {
                forward = toCamera;
                up = particle.Velocity.normalized;
                right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right);
            }
            else if ((_particleSystem.Flags & ParticleSystemFlags.TurnToVelocity) != 0)
            {
                forward = particle.Velocity.normalized;
                up = Vector3.up;
                right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right);
            }
            else
            {
                // No billboard
                forward = Vector3.forward;
                up = Vector3.up;
                right = Vector3.right;
            }
            
            // Apply rotation
            float cosRot = Mathf.Cos(particle.RotationZ);
            float sinRot = Mathf.Sin(particle.RotationZ);
            Vector3 rotRight = right * cosRot - up * sinRot;
            Vector3 rotUp = right * sinRot + up * cosRot;
            
            // Apply scale
            float scale = particle.Size * _particleSystem.GetScaleAtTime(particle.Time);
            rotRight *= scale;
            rotUp *= scale;
            forward *= scale;
            
            // Create matrix
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(rotRight.x, rotRight.y, rotRight.z, 0));
            matrix.SetColumn(1, new Vector4(rotUp.x, rotUp.y, rotUp.z, 0));
            matrix.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0));
            matrix.SetColumn(3, new Vector4(particle.Position.x, particle.Position.y, particle.Position.z, 1));
            
            return matrix;
        }
        
        private void DrawParticleBatch()
        {
            if (_matrices.Count == 0)
                return;
                
            if (_matrices.Count == 1)
            {
                // Draw single particle
                _propertyBlock.SetColor("_TintColor", _colors[0]);
                Graphics.DrawMesh(_particleMesh, _matrices[0], _material, 0, null, 0, _propertyBlock);
            }
            else
            {
                // Draw particle batch
                // Note: This doesn't properly handle per-particle colors
                Matrix4x4[] matrixArray = _matrices.ToArray();
                Graphics.DrawMeshInstanced(_particleMesh, 0, _material, matrixArray, matrixArray.Length);
            }
            
            _matrices.Clear();
            _colors.Clear();
        }
    }
}