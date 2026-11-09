using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class RoomFirstDungeonGenerator : SimpleRandomWalkDungeonGenerator
{
    [SerializeField]
    private int minRoomWidth = 4, minRoomHeight = 4;
    [SerializeField]
    private int dungeonWidth = 20, dungeonHeight = 20;
    [SerializeField]
    private int floorsCount = 5;
    [SerializeField]
    [Range(0,10)]
    private int offset = 1;
    [SerializeField]
    private bool randomWalkRooms = false;
    [SerializeField]
    private int countTimesOfCreatingPlatforms = 3;
    [SerializeField]
    private float randomnessOfPlatformGenerator = 1;
    [SerializeField]
    private float strivingToRightDirectionOfPlatformGenerator = 1;
    [SerializeField]
    private float smoothnessOfPlatformGenerator = 1;
    [SerializeField]
    private int maxNumberOfPlatformNotInstalled = 4;
    [SerializeField]
    private float chanceOfPlatformCollapse = 0.33f;

    protected override void RunProceduralGeneration()
    {
        CreateRooms();
    }

    private void CreateRooms()
    {
        var floorsOfRoomsList = ProceduralGenerationAlgorithms.BinarySpacePartitioning(new BoundsInt((Vector3Int)startPosition, new Vector3Int(dungeonWidth, dungeonHeight, 1)), minRoomWidth, minRoomHeight, floorsCount);

        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();

        Vector2Int dotOfFloorUpper = new Vector2Int(Random.Range(floorsOfRoomsList[0][0].xMin, floorsOfRoomsList[0][0].xMax), floorsOfRoomsList[0][0].yMax + minRoomHeight/2);

        foreach (var roomsOnFloor in floorsOfRoomsList)
        {
            if (randomWalkRooms)
            {
                floor.UnionWith(CreateRoomsRandomly(roomsOnFloor));
            }
            else
            {
                floor.UnionWith(CreateSimpleRooms(roomsOnFloor));
            }

            List<Vector2Int> roomsDots = new List<Vector2Int>();
            roomsDots.Add(dotOfFloorUpper);
            foreach (var room in roomsOnFloor)
            {
                roomsDots.Add((Vector2Int)Vector3Int.RoundToInt(new UnityEngine.Vector3(room.center.x, Random.Range((float)room.yMin + 3, room.yMax - 3), room.center.z)));
            }

            if (roomsDots.Count > 0) dotOfFloorUpper = roomsDots[1];

            List<Vector2Int> roomsDotsCopy = new List<Vector2Int>(roomsDots);

            HashSet<Vector2Int> corridors = ConnectRooms(roomsDots);
            floor.UnionWith(corridors);

            for (int indexer = 0; indexer < countTimesOfCreatingPlatforms; indexer++)
            {
                List<Vector2Int> roomsDotsCopyCopy = new List<Vector2Int>(roomsDotsCopy);
                HashSet<Vector2Int> platforms = AddPlatforms(roomsDotsCopyCopy, roomsOnFloor);
                floor.ExceptWith(platforms);
            }

        }


        tilemapVisualizer.PaintFloorTiles(floor);
        WallGenerator.CreateWalls(floor, tilemapVisualizer);
    }

    private HashSet<Vector2Int> CreateRoomsRandomly(List<BoundsInt> roomsList)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        for (int i = 0; i < roomsList.Count; i++)
        {
            var roomBounds = roomsList[i];
            var roomCenter = new Vector2Int(Mathf.RoundToInt(roomBounds.center.x), Mathf.RoundToInt(roomBounds.center.y));
            var roomFloor = RunRandomWalk(randomWalkParameters, roomCenter);
            foreach (var position in roomFloor)
            {
                if(position.x >= (roomBounds.xMin + offset) && position.x <= (roomBounds.xMax - offset) && position.y >= (roomBounds.yMin - offset) && position.y <= (roomBounds.yMax - offset))
                {
                    floor.Add(position);
                }
            }
        }
        return floor;
    }

    private HashSet<Vector2Int> AddPlatforms(List<Vector2Int> roomCenters, List<BoundsInt> roomsOnFloor)
    {
        HashSet<Vector2Int> platforms = new HashSet<Vector2Int>();
        var currentRoomCenter = roomCenters[0];
        roomCenters.Remove(currentRoomCenter);

        var closestRoomOfDot = ClosestRoomOfDot(currentRoomCenter, roomsOnFloor);
        HashSet<Vector2Int> newPlatforms = CreateFirstPlatform(currentRoomCenter, closestRoomOfDot);
        platforms.UnionWith(newPlatforms);

        while (roomCenters.Count > 0)
        {
            Vector2Int closest = FindClosestPointTo(currentRoomCenter, roomCenters);
            BoundsInt roomOfClosest = RoomOfDot(closest, roomsOnFloor);
            BoundsInt currentRoom = RoomOfDot(currentRoomCenter, roomsOnFloor);
            roomCenters.Remove(closest);
            newPlatforms = CreatePlatformsByRandomWalker(currentRoomCenter, closest, roomOfClosest, currentRoom);
            currentRoomCenter = closest;
            platforms.UnionWith(newPlatforms);
        }
        return platforms;
    }

    public static Vector3Int ClosestPoint(BoundsInt bounds, Vector3Int p)
    {
        var closest = float.MaxValue;
        var closestPoint = Vector3Int.zero;
        foreach (var i in bounds.allPositionsWithin)
        {
            var test = (i - p).magnitude;
            if (test < closest)
            {
                closest = test;
                closestPoint = i;
            }
        }
        return closestPoint;
    }

    private HashSet<Vector2Int> CreateFirstPlatform(Vector2Int currentRoomCenter, BoundsInt firstRoom)
    {
        HashSet<Vector2Int> platform = new HashSet<Vector2Int>();

        int platformHeight = (int)Random.Range(firstRoom.yMin + firstRoom.size.y * 0.5f, firstRoom.yMax - 4);

        for (int i = -1; i <= 1; i++)
        {
            Vector2Int dotOfPlatform = new Vector2Int(currentRoomCenter.x + i, platformHeight);
            if (BoundsIntContainsDot(firstRoom, dotOfPlatform, 3)) 
                platform.Add(dotOfPlatform);
        }

        return platform;
    }

    private HashSet<Vector2Int> CreatePlatformsByRandomWalker(Vector2Int currentRoomCenter, Vector2Int destination, BoundsInt roomOfDestination, BoundsInt currentRoom)
    {
        HashSet<Vector2Int> platforms = new HashSet<Vector2Int>();
        int numberOfPlatformNotInstalled = 0;
        var position = currentRoomCenter;
        if (((BoundsIntContainsDot(roomOfDestination, position, 3) || BoundsIntContainsDot(currentRoom, position, 3)))
                && (Random.Range(0f, 1f) > chanceOfPlatformCollapse || numberOfPlatformNotInstalled > maxNumberOfPlatformNotInstalled))
        {
            platforms.Add(position);
            numberOfPlatformNotInstalled = 0;
        }
        UnityEngine.Vector2 currentDirection = (UnityEngine.Vector2)(destination - position);
        currentDirection.Normalize();
        currentDirection.x *= -1f; 

        while (position.y != destination.y || position.x != destination.x)
        {
            UnityEngine.Vector2 rightDirection = destination - position;
            rightDirection.Normalize();
            var randomDirection = Direction2D.GetRandomCardinalDirection();
            currentDirection = ((UnityEngine.Vector2)randomDirection * randomnessOfPlatformGenerator + rightDirection * strivingToRightDirectionOfPlatformGenerator + currentDirection * smoothnessOfPlatformGenerator);
            currentDirection.Normalize();
            position += new Vector2Int((int)Math.Round(currentDirection.x, MidpointRounding.AwayFromZero), (int)Math.Round(currentDirection.y, MidpointRounding.AwayFromZero));
            if ((BoundsIntContainsDot(roomOfDestination, position, 3) || BoundsIntContainsDot(currentRoom, position, 3))
                && (Random.Range(0f, 1f) > chanceOfPlatformCollapse || numberOfPlatformNotInstalled > maxNumberOfPlatformNotInstalled))
            {
                platforms.Add(position);
                numberOfPlatformNotInstalled = 0;
            }
            else numberOfPlatformNotInstalled++;

        }

        return platforms;
    }

    bool BoundsIntContainsDot(BoundsInt boundsInt, Vector2Int dot, int indent)
    {
        return (boundsInt.xMin + indent < dot.x && boundsInt.xMax - indent > dot.x && boundsInt.yMin + indent < dot.y && boundsInt.yMax - indent > dot.y);
    }

    BoundsInt RoomOfDot(Vector2Int dot, List<BoundsInt> rooms)
    {
        BoundsInt foundRoom = new BoundsInt();
        foreach (var room in rooms)
        {
            if (room.Contains(new Vector3Int(dot.x, dot.y, (int)room.center.z)))
            {
                foundRoom = room;
                break;
            }
        }
        return foundRoom;
    }

    BoundsInt ClosestRoomOfDot(Vector2Int dot, List<BoundsInt> rooms)
    {
        BoundsInt foundRoom = new BoundsInt();
        if (rooms.Count == 0) return foundRoom;

        double minDistanceToRoom = UnityEngine.Vector3.Distance(ClosestPoint(rooms[0], (Vector3Int)dot), new UnityEngine.Vector3(dot.x, dot.y));
        foundRoom = rooms[0];

        foreach (var room in rooms)
        {
            if (UnityEngine.Vector3.Distance(ClosestPoint(room, (Vector3Int)dot), new UnityEngine.Vector3(dot.x,dot.y)) < minDistanceToRoom)
            {
                foundRoom = room;
                minDistanceToRoom = UnityEngine.Vector3.Distance(ClosestPoint(room, (Vector3Int)dot), new UnityEngine.Vector3(dot.x, dot.y));
            }
        }
        return foundRoom;
    }

    private HashSet<Vector2Int> ConnectRooms(List<Vector2Int> roomCenters)
    {
        HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        var currentRoomCenter = roomCenters[0];
        roomCenters.Remove(currentRoomCenter);

        while (roomCenters.Count > 0)
        {
            Vector2Int closest = FindClosestPointTo(currentRoomCenter, roomCenters);
            roomCenters.Remove(closest);
            HashSet<Vector2Int> newCorridor = CreateCorridor(currentRoomCenter, closest);
            currentRoomCenter = closest;
            corridors.UnionWith(newCorridor);
        }
        return corridors;
    }

    private HashSet<Vector2Int> CreateCorridor(Vector2Int currentRoomCenter, Vector2Int destination)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();
        var position = currentRoomCenter;
        corridor.Add(position);
        while (position.y != destination.y)
        {
            if(destination.y > position.y)
            {
                position += Vector2Int.up;
            }
            else if(destination.y < position.y)
            {
                position += Vector2Int.down;
            }
            corridor.Add(position);
        }
        while (position.x != destination.x)
        {
            if (destination.x > position.x)
            {
                position += Vector2Int.right;
            }else if(destination.x < position.x)
            {
                position += Vector2Int.left;
            }
            corridor.Add(position);
        }
        return corridor;
    }

    private Vector2Int FindClosestPointTo(Vector2Int currentRoomCenter, List<Vector2Int> roomCenters)
    {
        Vector2Int closest = Vector2Int.zero;
        float distance = float.MaxValue;
        foreach (var position in roomCenters)
        {
            float currentDistance = UnityEngine.Vector2.Distance(position, currentRoomCenter);
            if(currentDistance < distance)
            {
                distance = currentDistance;
                closest = position;
            }
        }
        return closest;
    }

    private HashSet<Vector2Int> CreateSimpleRooms(List<BoundsInt> roomsList)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (var room in roomsList)
        {
            for (int col = offset; col < room.size.x - offset; col++)
            {
                for (int row = offset; row < room.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);
                    floor.Add(position);
                }
            }
        }
        return floor;
    }
}
