static const int3 offsets3D[27] =
{
	int3(-1, -1, -1),
	int3(-1, -1, 0),
	int3(-1, -1, 1),
	int3(-1, 0, -1),
	int3(-1, 0, 0),
	int3(-1, 0, 1),
	int3(-1, 1, -1),
	int3(-1, 1, 0),
	int3(-1, 1, 1),
	int3(0, -1, -1),
	int3(0, -1, 0),
	int3(0, -1, 1),
	int3(0, 0, -1),
	int3(0, 0, 0),
	int3(0, 0, 1),
	int3(0, 1, -1),
	int3(0, 1, 0),
	int3(0, 1, 1),
	int3(1, -1, -1),
	int3(1, -1, 0),
	int3(1, -1, 1),
	int3(1, 0, -1),
	int3(1, 0, 0),
	int3(1, 0, 1),
	int3(1, 1, -1),
	int3(1, 1, 0),
	int3(1, 1, 1)
};

static const uint hash1 = 15823;
static const uint hash2 = 9737333;
static const uint hash3 = 440817757;

// 粒子の位置からセルのインデックスを取得
inline int3 GetCell(float3 position, float gridSize)
{
    return (int3)floor(position / gridSize);
}

// セルの位置からハッシュを計算
inline uint GetHash(int3 cell)
{
    // 50セルで1ブロックとする
    const uint blockSize = 50;
    uint3 ucell = (uint3)(cell + blockSize / 2);

    // ブロック内のインデックス
    uint3 localCell = ucell % blockSize;
    // ブロックのインデックス
    uint3 blockID = ucell / blockSize;
    uint blockHash = blockID.x * hash1 + blockID.y * hash2 + blockID.z * hash3;
    return localCell.x + blockSize * localCell.y + blockSize * blockSize * localCell.z + blockHash;
}

uint GetKey(uint hash, uint tableSize)
{
    return hash % tableSize;
}