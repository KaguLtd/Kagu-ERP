package com.kagultd.erp.ui

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import com.kagultd.erp.ui.theme.KaguErpTheme
import org.junit.Rule
import org.junit.Test

class KaguErpAppTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun showsContextAndMobileScope() {
        composeRule.setContent {
            KaguErpTheme {
                KaguErpApp()
            }
        }

        composeRule.onNodeWithText("Şirket").assertIsDisplayed()
        composeRule.onNodeWithText("Henüz seçilmedi").assertIsDisplayed()
        composeRule.onNodeWithText("Güvenli mobil çalışma alanı").assertIsDisplayed()
    }
}
