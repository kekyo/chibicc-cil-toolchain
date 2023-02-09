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
            if (this.structureType!.Attributes.HasFlag(TypeAttributes.SequentialLayout))
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
                Debug.Assert(this.structureType!.Attributes.HasFlag(TypeAttributes.ExplicitLayout));

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

            // Checks existing structure member declaration.
            if (this.checkingMemberIndex >= 0)
            {
                var field = this.structureType!.Fields.
                    ElementAtOrDefault(this.checkingMemberIndex);

                if (field == null)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Structure member difference exists before declared type: {memberName}");
                }
                else if (field.Name != memberName)
                {
                    this.OutputError(
                        memberNameToken,
                        $"Structure member name difference exists before declared type: {field.Name}");
                }
                else if (field.Attributes != FieldAttributes.Public)
                {
                    this.OutputError(
                        memberTypeNameToken,
                        $"Structure member attributes difference exists before declared type: {field.Attributes}");
                }
                else if (memberOffset is { } mo &&
                    field.Offset != mo)
                {
                    this.OutputError(
                        memberTypeNameToken,
                        $"Structure member offset difference exists before declared type: {field.Offset}");
                }
                else
                {
                    var capturedField = field;
                    var capturedMemberTypeName = memberTypeName;
                    var capturedMemberTypeNameToken = memberTypeNameToken;
                    this.delayedCheckAfterLookingupActions.Add(() =>
                    {
                        // Checkup its structure member type.
                        if (!this.TryGetType(capturedMemberTypeName, out var memberType) ||
                            capturedField.FieldType.FullName != memberType.FullName)
                        {
                            this.OutputError(
                                capturedMemberTypeNameToken,
                                $"Structure member type difference exists before declared type: {capturedField.FieldType.FullName}");
                        }
                    });
                }

                this.checkingMemberIndex++;
            }
            // Create a field into this structure.
            else
            {
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

                this.structureType!.Fields.Add(field);
            }
        }
    }
}
