package com.example.iptv

import android.os.Bundle
import android.view.KeyEvent
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import com.example.iptv.ui.IptvApp
import com.example.iptv.ui.IptvViewModel

class MainActivity : ComponentActivity() {

    private val viewModel: IptvViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { IptvApp(viewModel) }
    }

    /**
     * Teclas del mando mientras se reproduce:
     *  - CH+/CH-: canal anterior/siguiente.
     *  - Cifras 0-9: saltar a un canal por su número.
     *  - "Último canal": volver al canal que se veía antes.
     */
    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.action == KeyEvent.ACTION_DOWN && viewModel.state.value.playing != null) {
            when (val code = event.keyCode) {
                KeyEvent.KEYCODE_CHANNEL_UP, KeyEvent.KEYCODE_PAGE_UP -> { viewModel.zap(-1); return true }
                KeyEvent.KEYCODE_CHANNEL_DOWN, KeyEvent.KEYCODE_PAGE_DOWN -> { viewModel.zap(+1); return true }
                KeyEvent.KEYCODE_LAST_CHANNEL -> { viewModel.playPrevious(); return true }
                in KeyEvent.KEYCODE_0..KeyEvent.KEYCODE_9 -> {
                    viewModel.typeDigit(code - KeyEvent.KEYCODE_0); return true
                }
                in KeyEvent.KEYCODE_NUMPAD_0..KeyEvent.KEYCODE_NUMPAD_9 -> {
                    viewModel.typeDigit(code - KeyEvent.KEYCODE_NUMPAD_0); return true
                }
            }
        }
        return super.dispatchKeyEvent(event)
    }
}
