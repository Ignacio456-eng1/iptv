package com.example.iptv.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.StarBorder
import androidx.compose.material.icons.filled.Tv
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import com.example.iptv.data.Channel

@Composable
fun ChannelsScreen(
    state: UiState,
    onSelectGroup: (String) -> Unit,
    onQuery: (String) -> Unit,
    onPlay: (Channel) -> Unit,
    onToggleFavorite: (Channel) -> Unit,
    onRefresh: () -> Unit,
    onShowPlaylists: () -> Unit,
    onDismissError: () -> Unit,
) {
    val channels = state.visibleChannels

    Column(Modifier.fillMaxSize()) {
        // Lista activa: al pulsar se abre "Mis listas" para cambiar de lista.
        Row(
            Modifier
                .fillMaxWidth()
                .clickable(onClick = onShowPlaylists)
                .padding(start = 16.dp, end = 8.dp, top = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f)) {
                Text("Lista", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(
                    state.active?.name ?: "",
                    style = MaterialTheme.typography.titleLarge,
                    maxLines = 1, overflow = TextOverflow.Ellipsis,
                )
            }
            TextButton(onClick = onShowPlaylists) {
                Icon(Icons.AutoMirrored.Filled.List, null)
                Spacer(Modifier.width(6.dp))
                Text("Mis listas (${state.playlists.size})")
            }
        }

        Row(
            Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedTextField(
                value = state.query,
                onValueChange = onQuery,
                placeholder = { Text("Buscar canal") },
                leadingIcon = { Icon(Icons.Default.Search, null) },
                singleLine = true,
                modifier = Modifier.weight(1f),
            )
            IconButton(onClick = onRefresh) { Icon(Icons.Default.Refresh, "Actualizar lista") }
        }

        if (state.loading) LinearProgressIndicator(Modifier.fillMaxWidth())

        LazyRow(
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(state.groups) { group ->
                FilterChip(
                    selected = group == state.selectedGroup,
                    onClick = { onSelectGroup(group) },
                    label = { Text(group, maxLines = 1) },
                )
            }
        }

        Text(
            "${channels.size} canales",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
        )

        LazyColumn(
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, bottom = 16.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            items(channels, key = { it.key }) { ch ->
                ChannelRow(
                    channel = ch,
                    favorite = ch.key in state.favorites,
                    onClick = { onPlay(ch) },
                    onToggleFavorite = { onToggleFavorite(ch) },
                )
            }
        }
    }

    if (state.error != null) {
        AlertDialog(
            onDismissRequest = onDismissError,
            confirmButton = { TextButton(onClick = onDismissError) { Text("Aceptar") } },
            title = { Text("No se pudo actualizar la lista") },
            text = { Text(state.error) },
        )
    }
}

@Composable
private fun ChannelRow(
    channel: Channel,
    favorite: Boolean,
    onClick: () -> Unit,
    onToggleFavorite: () -> Unit,
) {
    // Resaltado visible al navegar con el mando (Android TV).
    val interaction = remember { MutableInteractionSource() }
    val focused by interaction.collectIsFocusedAsState()
    val shape = RoundedCornerShape(10.dp)

    Row(
        Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(if (focused) MaterialTheme.colorScheme.surfaceVariant else MaterialTheme.colorScheme.surface)
            .border(2.dp, if (focused) MaterialTheme.colorScheme.primary else Color.Transparent, shape)
            .clickable(interactionSource = interaction, indication = null, onClick = onClick)
            .padding(10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier.size(width = 64.dp, height = 40.dp).clip(RoundedCornerShape(6.dp))
                .background(Color(0xFF2A2A33)),
            contentAlignment = Alignment.Center,
        ) {
            if (channel.logo != null) {
                AsyncImage(
                    model = channel.logo, contentDescription = null,
                    contentScale = ContentScale.Fit, modifier = Modifier.fillMaxSize().padding(4.dp),
                )
            } else {
                Icon(Icons.Default.Tv, null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(channel.name, maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.titleMedium)
            channel.group?.let {
                Text(it, maxLines = 1, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        IconButton(onClick = onToggleFavorite) {
            Icon(
                if (favorite) Icons.Default.Star else Icons.Default.StarBorder,
                contentDescription = if (favorite) "Quitar de favoritos" else "Añadir a favoritos",
                tint = if (favorite) Color(0xFFFFC107) else MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
