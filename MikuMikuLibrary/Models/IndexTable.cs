﻿using MikuMikuLibrary.IO;
using MikuMikuLibrary.IO.Common;
using MikuMikuLibrary.IO.Sections;
using System;
using System.Collections.Generic;

namespace MikuMikuLibrary.Models
{
    public enum IndexTablePrimitiveType
    {
        Triangles = 4,
        TriangleStrip = 5,
    };

    public struct Triangle
    {
        public ushort A, B, C;

        public Triangle( ushort a, ushort b, ushort c )
        {
            A = a; B = b; C = c;
        }
    }

    public class IndexTable
    {
        public static int ByteSize( BinaryFormat format )
        {
            switch ( format )
            {
                case BinaryFormat.DT:
                case BinaryFormat.F:
                case BinaryFormat.FT:
                    return 0x5C;

                case BinaryFormat.F2nd:
                    return 0x70;

                case BinaryFormat.X:
                    return 0x80;
            }

            throw new ArgumentException( nameof( format ) );
        }

        public BoundingSphere BoundingSphere { get; set; }
        public ushort[] Indices { get; set; }
        public ushort[] BoneIndices { get; set; }
        public int MaterialIndex { get; set; }
        public IndexTablePrimitiveType PrimitiveType { get; set; }

        // Modern Formats
        public BoundingBox BoundingBox { get; set; }
        public int Field00 { get; set; }

        internal void Read( EndianBinaryReader reader, MeshSection section = null )
        {
            reader.SeekCurrent( 4 );
            BoundingSphere = reader.ReadBoundingSphere();
            MaterialIndex = reader.ReadInt32();
            reader.SeekCurrent( 8 );
            int boneIndexCount = reader.ReadInt32();
            long boneIndicesOffset = reader.ReadOffset();
            uint field00 = reader.ReadUInt32();
            PrimitiveType = ( IndexTablePrimitiveType )reader.ReadUInt32();
            int field01 = reader.ReadInt32();
            int indexCount = reader.ReadInt32();
            uint indicesOffset = reader.ReadUInt32();

            if ( section != null )
            {
                reader.SeekCurrent( section.Format == BinaryFormat.X ? 0x18 : 0x14 );
                BoundingBox = reader.ReadBoundingBox();
                Field00 = reader.ReadInt32();
            }

            reader.ReadAtOffsetIf( field00 == 4, boneIndicesOffset, () =>
            {
                BoneIndices = reader.ReadUInt16s( boneIndexCount );
            } );

            if ( section == null )
            {
                reader.ReadAtOffset( indicesOffset, () =>
                {
                    Indices = reader.ReadUInt16s( indexCount );
                } );
            }
            else
            {
                section.IndexData.Reader.SeekBegin( indicesOffset );
                Indices = section.IndexData.Reader.ReadUInt16s( indexCount );
            }
        }

        internal void Write( EndianBinaryWriter writer, MeshSection section = null )
        {
            writer.Write( 0 );
            writer.Write( BoundingSphere );
            writer.Write( MaterialIndex );
            writer.WriteNulls( 8 );
            writer.Write( BoneIndices != null ? BoneIndices.Length : 0 );
            writer.EnqueueOffsetWriteIf( BoneIndices != null, 4, AlignmentKind.Left, () =>
            {
                writer.Write( BoneIndices );
            } );
            writer.Write( BoneIndices != null ? 4 : 0 );
            writer.Write( ( int )PrimitiveType );
            writer.Write( 1 );
            writer.Write( Indices.Length );

            // Modern Format
            if ( section != null )
            {
                section.IndexData.Writer.WriteAlignmentPadding( 4 );
                writer.Write( ( uint )section.IndexData.Data.Position );

                // Write the indices to the index data
                section.IndexData.Writer.Write( Indices );

                writer.WriteNulls( 20 );
                writer.Write( BoundingBox );
                writer.Write( Field00 );
                writer.Write( 0 );
            }

            else
            {
                writer.EnqueueOffsetWrite( 4, AlignmentKind.Left, () =>
                {
                    writer.Write( Indices );
                } );

                writer.WriteNulls( 32 );
            }
        }

        public unsafe List<Triangle> GetTriangles()
        {
            var triangles = new List<Triangle>();
            if ( Indices == null || Indices.Length == 0 )
                return triangles;

            fixed ( ushort* indicesPtr = Indices )
            {
                ushort* start = indicesPtr;
                ushort* end = start + Indices.Length;

                if ( PrimitiveType == IndexTablePrimitiveType.Triangles )
                {
                    while ( start < end )
                        triangles.Add( new Triangle( *start++, *start++, *start++ ) );
                }
                else if ( PrimitiveType == IndexTablePrimitiveType.TriangleStrip )
                {
                    ushort a = *start++; ushort b = *start++; ushort c = 0;
                    int direction = -1;

                    while ( start < end )
                    {
                        c = *start++;

                        if ( c == 0xFFFF )
                        {
                            a = *start++;
                            b = *start++;
                            direction = -1;
                        }

                        else
                        {
                            direction *= -1;
                            if ( a != b && b != c && c != a )
                            {
                                if ( direction > 0 )
                                    triangles.Add( new Triangle( a, b, c ) );
                                else
                                    triangles.Add( new Triangle( a, c, b ) );
                            }

                            a = b;
                            b = c;
                        }
                    }
                }
            }

            return triangles;
        }

        public IndexTable()
        {
            PrimitiveType = IndexTablePrimitiveType.Triangles;
        }
    }
}
