package com.kagultd.erp.model

data class WorkingContext(
    val companyName: String?,
    val branchName: String?,
    val periodName: String?,
    val currencyCode: String?,
) {
    val isSelected: Boolean
        get() = companyName != null && branchName != null && periodName != null && currencyCode != null

    companion object {
        val Empty = WorkingContext(
            companyName = null,
            branchName = null,
            periodName = null,
            currencyCode = null,
        )
    }
}

