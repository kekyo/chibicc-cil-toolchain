/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Diagnostics;
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
    private void ParseStructureMember(Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing member operand.");
        }
        else if (tokens.Length > 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else
        {
            var memberTypeNameToken = tokens[0];
            var memberTypeName = memberTypeNameToken.Text;
            var memberNameToken = tokens[1];
            var memberName = memberNameToken.Text;

            int? memberOffset = null;
            if (this.structure!.Attributes.HasFlag(TypeAttributes.SequentialLayout))
            {
                if (tokens.Length == 3)
                {
                    this.OutputError(
                        tokens[2],
                        $"Could not apply member offset: {tokens[2].Text}");
                }
            }
            else
            {
                Debug.Assert(this.structure!.Attributes.HasFlag(TypeAttributes.ExplicitLayout));

                if (tokens.Length == 2)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Missing member offset operand: {memberName}");
                }
                else if (!int.TryParse(tokens[2].Text, out var offset) ||
                    offset < 0)
                {
                    this.OutputError(
                        tokens[2],
                        $"Invalid member offset: {tokens[2].Text}");
                }
                else
                {
                    memberOffset = offset;
                }
            }

            FieldDefinition field = null!;
            if (!this.TryGetType(memberTypeName, out var memberType))
            {
                memberType = this.CreateDummyType();

                this.DelayLookingUpType(
                    memberTypeNameToken,
                    type => field.FieldType = type);
            }

            field = new FieldDefinition(
                memberName, FieldAttributes.Public, memberType);

            if (memberOffset is { } mo)
            {
                field.Offset = mo;
            }

            this.structure!.Fields.Add(field);
        }
    }
}
