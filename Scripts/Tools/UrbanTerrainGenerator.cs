/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Procedural urban terrain generator for the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DECIDE.Terrain {
    /// <summary>
    /// Generates urban terrain with buildings, streets, and props
    /// </summary>
    public class UrbanTerrainGenerator : MonoBehaviour {
        [Header("Terrain Settings")]
        [SerializeField] private Vector2 _terrainSize = new Vector2(100f, 100f);
        [SerializeField] private Material _groundMaterial;
        [SerializeField] private Material _roadMaterial;
        [SerializeField] private float _blockSize = 20f;
        [SerializeField] private float _streetWidth = 6f;
        
        [Header("Building Settings")]
        [SerializeField] private int _minBuildingsPerBlock = 2;
        [SerializeField] private int _maxBuildingsPerBlock = 4;
        [SerializeField] private Vector2 _buildingSizeRange = new Vector2(5f, 15f);
        [SerializeField] private Vector2 _buildingHeightRange = new Vector2(10f, 30f);
        [SerializeField] private Material[] _buildingMaterials;
        
        [Header("Props")]
        [SerializeField] private bool _generateProps = true;
        [SerializeField] private int _treesPerBlock = 3;
        [SerializeField] private int _benchesPerBlock = 2;
        [SerializeField] private int _lampsPerBlock = 4;
        [SerializeField] private int _signsPerBlock = 2;
        
        [Header("Navigation")]
        [SerializeField] private bool _generateNavMesh = true;
        
        // Generated objects containers
        private Transform _buildingsContainer;
        private Transform _streetsContainer;
        private Transform _propsContainer;
        private List<GameObject> _allGeneratedObjects;
        
        private void Awake() {
            _allGeneratedObjects = new List<GameObject>();
        }
        
        /// <summary>
        /// Main generation method
        /// </summary>
        public void GenerateTerrain() {
            ClearExistingTerrain();
            CreateContainers();
            GenerateGround();
            GenerateStreetGrid();
            GenerateCityBlocks();
            if (_generateProps) {
                GenerateProps();
            }
            if (_generateNavMesh) {
                BakeNavMesh();
            }
        }
        
        /// <summary>
        /// Clears any existing terrain
        /// </summary>
        public void ClearExistingTerrain() {
            foreach (var obj in _allGeneratedObjects) {
                if (obj != null) {
                    DestroyImmediate(obj);
                }
            }
            _allGeneratedObjects.Clear();
            
            // Destroy containers
            if (_buildingsContainer != null) DestroyImmediate(_buildingsContainer.gameObject);
            if (_streetsContainer != null) DestroyImmediate(_streetsContainer.gameObject);
            if (_propsContainer != null) DestroyImmediate(_propsContainer.gameObject);
        }
        
        /// <summary>
        /// Creates organizational containers
        /// </summary>
        private void CreateContainers() {
            _buildingsContainer = new GameObject("Buildings").transform;
            _buildingsContainer.SetParent(transform);
            
            _streetsContainer = new GameObject("Streets").transform;
            _streetsContainer.SetParent(transform);
            
            _propsContainer = new GameObject("Props").transform;
            _propsContainer.SetParent(transform);
        }
        
        /// <summary>
        /// Generates the ground plane
        /// </summary>
        private void GenerateGround() {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(_terrainSize.x / 10f, 1f, _terrainSize.y / 10f);
            
            if (_groundMaterial != null) {
                ground.GetComponent<Renderer>().material = _groundMaterial;
            } else {
                ground.GetComponent<Renderer>().material.color = new Color(0.3f, 0.3f, 0.3f);
            }
            
            // Add as static for nav mesh
            ground.isStatic = true;
            _allGeneratedObjects.Add(ground);
            
            // Create boundary walls
            CreateBoundaryWalls();
        }
        
        /// <summary>
        /// Creates boundary walls around the terrain
        /// </summary>
        private void CreateBoundaryWalls() {
            float wallHeight = 5f;
            float wallThickness = 0.5f;
            
            // North wall
            CreateWall(new Vector3(0, wallHeight/2, _terrainSize.y/2),
                      new Vector3(_terrainSize.x, wallHeight, wallThickness));
            
            // South wall
            CreateWall(new Vector3(0, wallHeight/2, -_terrainSize.y/2),
                      new Vector3(_terrainSize.x, wallHeight, wallThickness));
            
            // East wall
            CreateWall(new Vector3(_terrainSize.x/2, wallHeight/2, 0),
                      new Vector3(wallThickness, wallHeight, _terrainSize.y));
            
            // West wall
            CreateWall(new Vector3(-_terrainSize.x/2, wallHeight/2, 0),
                      new Vector3(wallThickness, wallHeight, _terrainSize.y));
        }
        
        /// <summary>
        /// Creates a wall segment
        /// </summary>
        private void CreateWall(Vector3 position, Vector3 scale) {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BoundaryWall";
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.4f);
            wall.isStatic = true;
            _allGeneratedObjects.Add(wall);
        }
        
        /// <summary>
        /// Generates the street grid
        /// </summary>
        private void GenerateStreetGrid() {
            float totalBlockSize = _blockSize + _streetWidth;
            int blocksX = Mathf.FloorToInt(_terrainSize.x / totalBlockSize);
            int blocksZ = Mathf.FloorToInt(_terrainSize.y / totalBlockSize);
            
            // Create horizontal streets
            for (int z = 0; z <= blocksZ; z++) {
                float zPos = -_terrainSize.y/2 + z * totalBlockSize + _blockSize/2;
                CreateStreet(new Vector3(0, 0.01f, zPos),
                           new Vector3(_terrainSize.x, 0.1f, _streetWidth),
                           true);
            }
            
            // Create vertical streets
            for (int x = 0; x <= blocksX; x++) {
                float xPos = -_terrainSize.x/2 + x * totalBlockSize + _blockSize/2;
                CreateStreet(new Vector3(xPos, 0.01f, 0),
                           new Vector3(_streetWidth, 0.1f, _terrainSize.y),
                           false);
            }
            
            // Create intersections
            for (int x = 0; x <= blocksX; x++) {
                for (int z = 0; z <= blocksZ; z++) {
                    float xPos = -_terrainSize.x/2 + x * totalBlockSize + _blockSize/2;
                    float zPos = -_terrainSize.y/2 + z * totalBlockSize + _blockSize/2;
                    CreateIntersection(new Vector3(xPos, 0.01f, zPos));
                }
            }
        }
        
        /// <summary>
        /// Creates a street segment
        /// </summary>
        private void CreateStreet(Vector3 position, Vector3 scale, bool isHorizontal) {
            GameObject street = GameObject.CreatePrimitive(PrimitiveType.Cube);
            street.name = isHorizontal ? "Street_Horizontal" : "Street_Vertical";
            street.transform.SetParent(_streetsContainer);
            street.transform.position = position;
            street.transform.localScale = scale;
            
            if (_roadMaterial != null) {
                street.GetComponent<Renderer>().material = _roadMaterial;
            } else {
                street.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f);
            }
            
            street.isStatic = true;
            _allGeneratedObjects.Add(street);
            
            // Add street markings
            if (isHorizontal) {
                CreateStreetMarkings(position, scale.x, true);
            } else {
                CreateStreetMarkings(position, scale.z, false);
            }
        }
        
        /// <summary>
        /// Creates street markings
        /// </summary>
        private void CreateStreetMarkings(Vector3 streetPos, float length, bool isHorizontal) {
            int markingCount = Mathf.FloorToInt(length / 4f);
            GameObject markingContainer = new GameObject("Street Markings");
            markingContainer.transform.SetParent(_streetsContainer);
            
            for (int i = 0; i < markingCount; i++) {
                GameObject marking = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marking.name = "Marking";
                marking.transform.SetParent(markingContainer.transform);
                
                if (isHorizontal) {
                    marking.transform.position = streetPos + new Vector3(-length/2 + i * 4f + 2f, 0.02f, 0);
                    marking.transform.localScale = new Vector3(2f, 0.01f, 0.1f);
                } else {
                    marking.transform.position = streetPos + new Vector3(0, 0.02f, -length/2 + i * 4f + 2f);
                    marking.transform.localScale = new Vector3(0.1f, 0.01f, 2f);
                }
                
                marking.GetComponent<Renderer>().material.color = Color.white;
                marking.isStatic = true;
                _allGeneratedObjects.Add(marking);
            }
        }
        
        /// <summary>
        /// Creates a street intersection
        /// </summary>
        private void CreateIntersection(Vector3 position) {
            GameObject intersection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intersection.name = "Intersection";
            intersection.transform.SetParent(_streetsContainer);
            intersection.transform.position = position;
            intersection.transform.localScale = new Vector3(_streetWidth, 0.1f, _streetWidth);
            
            if (_roadMaterial != null) {
                intersection.GetComponent<Renderer>().material = _roadMaterial;
            } else {
                intersection.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f);
            }
            
            intersection.isStatic = true;
            _allGeneratedObjects.Add(intersection);
        }
        
        /// <summary>
        /// Generates city blocks with buildings
        /// </summary>
        private void GenerateCityBlocks() {
            float totalBlockSize = _blockSize + _streetWidth;
            int blocksX = Mathf.FloorToInt(_terrainSize.x / totalBlockSize);
            int blocksZ = Mathf.FloorToInt(_terrainSize.y / totalBlockSize);
            
            for (int x = 0; x < blocksX; x++) {
                for (int z = 0; z < blocksZ; z++) {
                    Vector3 blockCenter = new Vector3(
                        -_terrainSize.x/2 + x * totalBlockSize + totalBlockSize/2,
                        0,
                        -_terrainSize.y/2 + z * totalBlockSize + totalBlockSize/2
                    );
                    
                    GenerateBuildingsInBlock(blockCenter, _blockSize - 2f);
                }
            }
        }
        
        /// <summary>
        /// Generates buildings within a city block
        /// </summary>
        private void GenerateBuildingsInBlock(Vector3 center, float blockSize) {
            int buildingCount = Random.Range(_minBuildingsPerBlock, _maxBuildingsPerBlock + 1);
            
            for (int i = 0; i < buildingCount; i++) {
                Vector3 randomPos = center + new Vector3(
                    Random.Range(-blockSize/2, blockSize/2),
                    0,
                    Random.Range(-blockSize/2, blockSize/2)
                );
                
                CreateBuilding(randomPos);
            }
        }
        
        /// <summary>
        /// Creates a single building
        /// </summary>
        private void CreateBuilding(Vector3 position) {
            float width = Random.Range(_buildingSizeRange.x, _buildingSizeRange.y);
            float depth = Random.Range(_buildingSizeRange.x, _buildingSizeRange.y);
            float height = Random.Range(_buildingHeightRange.x, _buildingHeightRange.y);
            
            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building";
            building.transform.SetParent(_buildingsContainer);
            building.transform.position = position + Vector3.up * height/2;
            building.transform.localScale = new Vector3(width, height, depth);
            
            // Apply random material
            if (_buildingMaterials != null && _buildingMaterials.Length > 0) {
                building.GetComponent<Renderer>().material = _buildingMaterials[Random.Range(0, _buildingMaterials.Length)];
            } else {
                Color buildingColor = new Color(
                    Random.Range(0.4f, 0.7f),
                    Random.Range(0.4f, 0.7f),
                    Random.Range(0.4f, 0.7f)
                );
                building.GetComponent<Renderer>().material.color = buildingColor;
            }
            
            building.isStatic = true;
            _allGeneratedObjects.Add(building);
            
            // Add windows
            AddWindowsToBuilding(building, width, height, depth);
        }
        
        /// <summary>
        /// Adds window details to a building
        /// </summary>
        private void AddWindowsToBuilding(GameObject building, float width, float height, float depth) {
            int floorsCount = Mathf.FloorToInt(height / 3f);
            int windowsPerFloor = Mathf.FloorToInt(width / 2f);
            
            GameObject windowContainer = new GameObject("Windows");
            windowContainer.transform.SetParent(building.transform);
            
            for (int floor = 0; floor < floorsCount; floor++) {
                for (int window = 0; window < windowsPerFloor; window++) {
                    // Front windows
                    CreateWindow(
                        building.transform.position + new Vector3(
                            -width/2 + (window + 0.5f) * (width/windowsPerFloor),
                            -height/2 + (floor + 0.5f) * 3f,
                            depth/2 + 0.01f
                        ),
                        windowContainer.transform
                    );
                    
                    // Back windows
                    CreateWindow(
                        building.transform.position + new Vector3(
                            -width/2 + (window + 0.5f) * (width/windowsPerFloor),
                            -height/2 + (floor + 0.5f) * 3f,
                            -depth/2 - 0.01f
                        ),
                        windowContainer.transform
                    );
                }
            }
        }
        
        /// <summary>
        /// Creates a window on a building
        /// </summary>
        private void CreateWindow(Vector3 position, Transform parent) {
            GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "Window";
            window.transform.SetParent(parent);
            window.transform.position = position;
            window.transform.localScale = new Vector3(1.5f, 2f, 0.1f);
            
            Material windowMat = new Material(Shader.Find("Standard"));
            windowMat.color = new Color(0.3f, 0.5f, 0.7f, 0.8f);
            windowMat.SetFloat("_Metallic", 0.5f);
            windowMat.SetFloat("_Glossiness", 0.8f);
            window.GetComponent<Renderer>().material = windowMat;
            
            window.isStatic = true;
            _allGeneratedObjects.Add(window);
        }
        
        /// <summary>
        /// Generates props like trees, benches, etc.
        /// </summary>
        private void GenerateProps() {
            float totalBlockSize = _blockSize + _streetWidth;
            int blocksX = Mathf.FloorToInt(_terrainSize.x / totalBlockSize);
            int blocksZ = Mathf.FloorToInt(_terrainSize.y / totalBlockSize);
            
            for (int x = 0; x < blocksX; x++) {
                for (int z = 0; z < blocksZ; z++) {
                    Vector3 blockCenter = new Vector3(
                        -_terrainSize.x/2 + x * totalBlockSize + totalBlockSize/2,
                        0,
                        -_terrainSize.y/2 + z * totalBlockSize + totalBlockSize/2
                    );
                    
                    // Trees
                    for (int i = 0; i < _treesPerBlock; i++) {
                        CreateTree(GetRandomPositionInBlock(blockCenter, _blockSize));
                    }
                    
                    // Benches
                    for (int i = 0; i < _benchesPerBlock; i++) {
                        CreateBench(GetRandomPositionInBlock(blockCenter, _blockSize));
                    }
                    
                    // Street lamps
                    for (int i = 0; i < _lampsPerBlock; i++) {
                        CreateStreetLamp(GetRandomStreetPosition(blockCenter, _blockSize, _streetWidth));
                    }
                    
                    // Signs
                    for (int i = 0; i < _signsPerBlock; i++) {
                        CreateSign(GetRandomStreetPosition(blockCenter, _blockSize, _streetWidth));
                    }
                }
            }
        }
        
        /// <summary>
        /// Creates a tree prop
        /// </summary>
        private void CreateTree(Vector3 position) {
            GameObject tree = new GameObject("Tree");
            tree.transform.SetParent(_propsContainer);
            tree.transform.position = position;
            
            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = Vector3.up * 1.5f;
            trunk.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            trunk.GetComponent<Renderer>().material.color = new Color(0.4f, 0.2f, 0.1f);
            
            // Leaves
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(tree.transform);
            leaves.transform.localPosition = Vector3.up * 3.5f;
            leaves.transform.localScale = new Vector3(3f, 3f, 3f);
            leaves.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 0.2f);
            
            _allGeneratedObjects.Add(tree);
        }
        
        /// <summary>
        /// Creates a bench prop
        /// </summary>
        private void CreateBench(Vector3 position) {
            GameObject bench = new GameObject("Bench");
            bench.transform.SetParent(_propsContainer);
            bench.transform.position = position;
            bench.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            
            // Seat
            GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Seat";
            seat.transform.SetParent(bench.transform);
            seat.transform.localPosition = Vector3.up * 0.4f;
            seat.transform.localScale = new Vector3(2f, 0.1f, 0.5f);
            seat.GetComponent<Renderer>().material.color = new Color(0.5f, 0.3f, 0.1f);
            
            // Back
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Back";
            back.transform.SetParent(bench.transform);
            back.transform.localPosition = new Vector3(0, 0.7f, -0.2f);
            back.transform.localScale = new Vector3(2f, 0.6f, 0.1f);
            back.GetComponent<Renderer>().material.color = new Color(0.5f, 0.3f, 0.1f);
            
            _allGeneratedObjects.Add(bench);
        }
        
        /// <summary>
        /// Creates a street lamp prop
        /// </summary>
        private void CreateStreetLamp(Vector3 position) {
            GameObject lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(_propsContainer);
            lamp.transform.position = position;
            
            // Pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(lamp.transform);
            pole.transform.localPosition = Vector3.up * 2.5f;
            pole.transform.localScale = new Vector3(0.2f, 2.5f, 0.2f);
            pole.GetComponent<Renderer>().material.color = new Color(0.3f, 0.3f, 0.3f);
            
            // Light
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = "Light";
            light.transform.SetParent(lamp.transform);
            light.transform.localPosition = Vector3.up * 5f;
            light.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            Material lightMat = new Material(Shader.Find("Standard"));
            lightMat.color = Color.yellow;
            lightMat.SetFloat("_Metallic", 0f);
            lightMat.SetFloat("_Glossiness", 0f);
            lightMat.EnableKeyword("_EMISSION");
            lightMat.SetColor("_EmissionColor", Color.yellow * 0.5f);
            light.GetComponent<Renderer>().material = lightMat;
            
            _allGeneratedObjects.Add(lamp);
        }
        
        /// <summary>
        /// Creates a sign prop
        /// </summary>
        private void CreateSign(Vector3 position) {
            GameObject sign = new GameObject("Sign");
            sign.transform.SetParent(_propsContainer);
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            
            // Pole
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(sign.transform);
            pole.transform.localPosition = Vector3.up * 1f;
            pole.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            pole.GetComponent<Renderer>().material.color = Color.gray;
            
            // Sign board
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(sign.transform);
            board.transform.localPosition = Vector3.up * 2f;
            board.transform.localScale = new Vector3(1f, 1f, 0.1f);
            
            Color signColor = Random.Range(0f, 1f) > 0.5f ? Color.red : Color.blue;
            board.GetComponent<Renderer>().material.color = signColor;
            
            _allGeneratedObjects.Add(sign);
        }
        
        /// <summary>
        /// Gets a random position within a block
        /// </summary>
        private Vector3 GetRandomPositionInBlock(Vector3 center, float blockSize) {
            return center + new Vector3(
                Random.Range(-blockSize/2, blockSize/2),
                0,
                Random.Range(-blockSize/2, blockSize/2)
            );
        }
        
        /// <summary>
        /// Gets a random position along the street
        /// </summary>
        private Vector3 GetRandomStreetPosition(Vector3 blockCenter, float blockSize, float streetWidth) {
            bool onHorizontalStreet = Random.Range(0f, 1f) > 0.5f;
            
            if (onHorizontalStreet) {
                float x = Random.Range(-blockSize/2, blockSize/2);
                float z = Random.Range(0f, 1f) > 0.5f ? 
                    blockCenter.z + blockSize/2 + streetWidth/2 :
                    blockCenter.z - blockSize/2 - streetWidth/2;
                return new Vector3(blockCenter.x + x, 0, z);
            } else {
                float z = Random.Range(-blockSize/2, blockSize/2);
                float x = Random.Range(0f, 1f) > 0.5f ?
                    blockCenter.x + blockSize/2 + streetWidth/2 :
                    blockCenter.x - blockSize/2 - streetWidth/2;
                return new Vector3(x, 0, blockCenter.z + z);
            }
        }
        
        /// <summary>
        /// Bakes the navigation mesh
        /// </summary>
        private void BakeNavMesh() {
            // Note: NavMesh baking in runtime requires NavMeshSurface component
            // This is a placeholder for the actual implementation
            Debug.Log("NavMesh baking should be done in the Unity Editor using the Navigation window");
        }
    }
}