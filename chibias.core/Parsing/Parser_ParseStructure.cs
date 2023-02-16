﻿/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Cecil;
using System.Diagnostics;
using System.Linq;

namespace chibias.Parsing;

partial class Parser
{
    private void ParseStructureMember(Token[] tokens)
    {
        if (tokens.Length < 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing member operand.");
            return;
        }
        
        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
            return;
        }
        
        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[0].Text,
            out var scopeDescriptor) ||
            scopeDescriptor == ScopeDescriptors.File)
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

        var fieldAttribute = scopeDescriptor switch
        {
            ScopeDescriptors.Public => FieldAttributes.Public,
            _ => FieldAttributes.Assembly,
        };

        var memberTypeNameToken = tokens[1];
        var memberTypeName = memberTypeNameToken.Text;
        var memberNameToken = tokens[2];
        var memberName = memberNameToken.Text;

        int? memberOffset = null;
        if (this.structureType!.Attributes.HasFlag(TypeAttributes.SequentialLayout))
        {
            if (tokens.Length == 4)
            {
                this.OutputError(
                    tokens[3],
                    $"Could not apply member offset: {tokens[3].Text}");
                return;
            }
        }
        else
        {
            Debug.Assert(this.structureType!.Attributes.HasFlag(TypeAttributes.ExplicitLayout));

            if (tokens.Length == 3)
            {
                this.OutputError(
                    memberNameToken,
                    $"Missing member offset operand: {memberName}");
                return;
            }
            
            if (!Utilities.TryParseInt32(tokens[3].Text, out var offset) ||
                offset < 0)
            {
                this.OutputError(
                    tokens[3],
                    $"Invalid member offset: {tokens[3].Text}");
                return;
            }

            memberOffset = offset;
        }

        // Checks existing structure member declaration.
        if (this.checkingMemberIndex >= 0)
        {
            var sf = this.structureType!.Fields.
                ElementAtOrDefault(this.checkingMemberIndex);

            if (sf == null)
            {
                this.OutputError(
                    memberNameToken,
                    $"Structure member difference exists before declared type: {memberName}");
                return;
            }
            
            if (sf.Name != memberName)
            {
                this.OutputError(
                    memberNameToken,
                    $"Structure member name difference exists before declared type: {sf.Name}");
                return;
            }
            
            if ((sf.Attributes & fieldAttribute) != fieldAttribute)
            {
                this.OutputError(
                    memberTypeNameToken,
                    $"Structure member attributes difference exists before declared type: {sf.Attributes}");
                return;
            }
            
            if (memberOffset is { } mo &&
                sf.Offset != mo)
            {
                this.OutputError(
                    memberTypeNameToken,
                    $"Structure member offset difference exists before declared type: {sf.Offset}");
                return;
            }

            var capturedField = sf;
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

            this.checkingMemberIndex++;
            return;
        }

        // Create a field into this structure.
        FieldDefinition field = null!;
        if (!this.TryGetType(memberTypeName, out var memberType))
        {
            memberType = this.CreateDummyType();

            this.DelayLookingUpType(
                memberTypeNameToken,
                type => field.FieldType = type);
        }

        field = new FieldDefinition(
            memberName,
            fieldAttribute,
            memberType);

        if (memberOffset is { } mo2)
        {
            field.Offset = mo2;
        }

        this.structureType!.Fields.Add(field);
    }
}