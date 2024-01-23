; =============================================================================
; F_LZ65_Decompress
; Decompresses LZ65 packed data from ROM or RAM into a RAM buffer.
; - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
; Arguments
; _srcPtr : 16 bit address of compressed data. This can be in RAM or ROM
; _dstPtr : 16 bit address of decompress buffer. This can be in RAM
; * This is a scoped process, default ram usage is $00:$05
; =============================================================================

LZ65_BASE := $00

.proc F_LZ65_Decompress
    	RAW_FLAG = $00  ; RAW : Copy the next n bytes from source buffer to destination buffer.
    	REP_FLAG = $40	; REP : Copy n bytes from destination buffer at idx.
    	RLE_FLAG = $80	; RLE : Repeat the next source buffer byte n times.
    	EOS_FLAG = $C0	; EOS : End of Stream
    	COM_MASK = $C0	; Command mask bits.
    	LEN_MASK = $3F	; Length mask bits.

    	_srcPtr     := LZ65_BASE      ; (word) Source data pointer to RAM/ROM
    	_srcPtrHi   := LZ65_BASE + 1  ;

    	_dstPtr     := LZ65_BASE + 2  ; (word) Destination data pointer to RAM
    	_dstPtrHi   := LZ65_BASE + 3  ; 
    
    	_tempOfs    := LZ65_BASE + 4  ; Temp value for offsets
    	_length     := LZ65_BASE + 5  ; Length counter

    	; -- Initialize --
    	    ldy #$00
			ldx #$00
    	    sty _tempOfs

    	; -- Fetch Command / Length bits --
		;   Decoders should assume Y and X are current srcPtr and dstPtr offsets.
		;   they should return Y and X as the current srcPtr and dstPtr offsets.
    	fetchComByte:
    	    lda (_srcPtr), y
    	    iny
            pha
    	    and #LEN_MASK
    	    sta _length     
			pla	
    	    and #COM_MASK	

    	; -- Select Decode Mode --
    	    cmp #RAW_FLAG
    	        beq decodeRaw
    	    cmp #RLE_FLAG
    	        beq decodeRle
    	    cmp #REP_FLAG
    	        beq decodeRep 
    	    return:
    	        rts ; EOS_FLAG is the only possiblity if we got here

    	; -- Decode Raw --
    	; Write the next _length bytes from _srcPtr[] to _dstPtr[]    
    	decodeRaw:
    	    _writeRaw:
    	        lda (_srcPtr), y
    	        sta _dstPtr, x
    	        iny
    	        inx
    	        dec _length
    	        bne _writeRaw
    	    jmp fetchComByte

    	; -- Decode Rle --
    	; Write the next byte _length times to _dstPtr[]
    	decodeRle:
    	    lda (_srcPtr), y
    	    iny
    	    writeRle:
    	        sta _dstPtr, x
    	        inx
    	        dec _length
    	        bne writeRle
    	    jmp fetchComByte

    	decodeRep:
    	    lda (_srcPtr), y
    	    iny
    	    sty _tempOfs	    ; Save Y because source REP data will be
    	    tay					; .. from _dstPtr[]
    	    writeRep:
    	        lda _dstPtr, y
    	        sta _dstPtr, x
    	        inx
    	        iny
    	        dec _length
    	        bne writeRep
			ldy _tempOfs		; Restore Y from  _tempOfs
    	    jmp fetchComByte

.endproc