package com.kagultd.erp.model

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class WorkingContextTest {
    @Test
    fun emptyContextIsNotSelected() {
        assertFalse(WorkingContext.Empty.isSelected)
    }

    @Test
    fun completeContextIsSelected() {
        val context = WorkingContext(
            companyName = "Sentetik Şirket",
            branchName = "Merkez",
            periodName = "2026",
            currencyCode = "TRY",
        )

        assertTrue(context.isSelected)
    }
}

